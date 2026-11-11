export const meta = {
  name: 'm7-g2-v2',
  description: 'G2 graphics demonstrator, rebuilt after the container loss: setup, Victus harness + before package, renderer, characters, environment, era kit, shadows/AO/crowds, scenes A-E, critic loop, verify, after package + visual review (sequential, segmented)',
  whenToUse: 'D-078 / D-079 DR43-DR50 graphics pillar in prototypes/m7-gfx2',
  phases: [
    { title: 'Setup' },
    { title: 'Stage 1' },
    { title: 'Stage 2' },
    { title: 'Stage 3' },
    { title: 'Scenes' },
    { title: 'Critic loop' },
    { title: 'Verify' },
    { title: 'Package' },
  ],
}

const SP = '/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad'
const DEV = '/home/user/civDemo'
const BASE = (args && args.base) || '1601c74'
const G = '/home/user/g2w'
const TRUNK = `${G}/trunk`
const SEGS = ['stage1', 'stage2', 'stage3', 'final']
const FROM = (args && args.from) || 'stage1'
const STOP = args && args.stopAfter
const wants = (s) => SEGS.indexOf(s) >= SEGS.indexOf(FROM)

const COMMON = `
## Context (shared by every G2 agent)
- Goal (Director: D-078 graphics quality mandate; D-079 DR43-DR50; D-077): move the M7 battle graphics from placeholder art to at least the visual quality of GTA San Andreas now, with Total War: Troy as the longer-term battlefield ambition. These names are quality references only (D-078 QM2 READING; D-069 originality): no asset, character, art, likeness, level, UI or look from them is used or recreated, and nothing player-facing names another game. MonoGame 3D stays (DR44; do not propose Godot). Read D-078 in full and D-079 DR39-DR50 before starting.
- Demonstrator (DR45-DR47, DR49): detailed soldier, commander and avatar (anatomy and proportions, faces, eyes, skin, hair, clothing, armour, weapons, shields, accessories), multiple soldier types and at least two opposing forces, a forest, a settlement and a fortification, terrain materials, lighting, shadows, atmosphere. Character pipeline: skeletal animation, blending, locomotion, combat, weapon handling, hit reactions, death, surrender, facial expressions, era-specific clothing, an avatar identity that persists across eras. Use high-quality authored base assets with procedural variation: variation multiplies quality, never low-quality geometry. Rendering (DR46): PBR with normal/roughness/metallic maps, cascaded shadows, anti-aliasing, ambient occlusion, atmospheric fog, water, LOD, occlusion culling, instancing, GPU crowd animation. Priority order (DR49): 1 actual Victus benchmark; 2 one production-quality authored asset from source through rendering, with provenance; 3 soldier and avatar demonstrator; 4 one detailed battlefield environment; then battle on the terrain grid, aftermath cinematics, weather.
- History: G1 (the BEFORE baseline, never modified) is ${DEV}/prototypes/m7-gfx1 (solution M7Gfx.slnx; reports/g1-summary.md; it can re-render its own captures). A first G2 attempt (2026-11-05/06) reached the end of stage 3 but was lost with its container before it was pushed; only its lessons, written into your stage task, survive. The Director already holds that attempt's Victus BEFORE package g2-before-c86bc7f.zip. G2 lives in prototypes/m7-gfx2 (solution M7Gfx2.slnx), copied forward from G1 by setup.
- HARD RULES: rendering never influences simulation (presentation reads published state or traces only; cosmetic animation never touches results, positions, health, RNG, timing or hashes; the avatar is purely visual; DR42). No permanent projects outside prototypes/. This container has NO GPU (llvmpipe software GL via SDL_VIDEODRIVER=offscreen LIBGL_ALWAYS_SOFTWARE=1): never claim hardware performance from it; label every number measured here 'llvmpipe, not Victus evidence'. Never call placeholder graphics production quality; say plainly what still looks placeholder.
- Assets and licences (DR47; D-079 Q8 licence policy OPEN): every asset gets a row in prototypes/m7-gfx2/assets/PROVENANCE.md (source URL or repository + commit, licence, licence evidence, status SHIP-OK / PROTOTYPE-ONLY / REJECTED, transformations). Working default: ship-clean first (CC0, MIT, BSD, CC-BY with attribution); PROTOTYPE-ONLY only where nothing clean exists, flagged in PROVENANCE and in every capture legend, swappable, never presented as shippable. CMU mocap is provisionally usable, flagged. Never commit large binaries: raw and baked assets live in ${G}/assets, produced by committed fetch and bake scripts plus manifests (prototypes/m7-gfx2/scripts, assets/manifest.tsv with sha256). Small JPEG captures for review (at most ~300 KB each, ~6 MB in total across G2) may be committed under prototypes/m7-gfx2/reports/captures.
- Sources the lost attempt found (re-verify each and pin sha256): MakeHuman hm08 CC0 core (base mesh, modelling targets including a 1,280-target npz, default rig, face pose units, eyes, skins, brow/lash/hair/mouth TEXTURES) and MPFB2 CC0 rigs (game_engine 53-bone), via GitHub mirrors; the MakeHuman 1.1.1 brow, lash, hair and mouth MESHES carry AGPL headers (PROTOTYPE-ONLY, replace with cards generated in code where you can); CMU mocap clips via GitHub mirrors (provisional terms); CC0 HDRI panoramas via GitHub mirrors; Terrain3D's Rock023 and Ground037 (MIT; ambientCG CC0 upstream inferred); BabylonJS ground textures (CC-BY-4.0, attribution); ez-tree (MIT) source and textures (oak and willow bark, leaves, grass card, flowers; birch and pine bark terms unverified, PROTOTYPE-ONLY); SMAA (MIT); meshoptimizer sources from its PyPI sdist (MIT) as a bake tool only. Rejected before: Ready Player Me, LaFAN1, Sponza, villagePack, others with unclear terms.
- Network (as of 2026-11-06; check once, do not retry denied hosts): reachable raw.githubusercontent.com, media.githubusercontent.com, codeload.github.com, npm, PyPI, NuGet, apt; denied: Poly Haven, ambientCG, Quaternius, OpenGameArt, the MakeHuman file server, Blender downloads, the CMU site, jsdelivr, unpkg, huggingface, archive.org, sketchfab, the github.com web UI and API.
- Machine: 4 CPUs, 15 GB RAM, about 30 GB free disk. Work on disk under ${G} (assets in ${G}/assets). A simulation lane runs concurrently: build with nice -n 19, -m:1, -nodeReuse:false and DOTNET_CLI_USE_MSBUILD_SERVER=0, naming the solution file. dotnet is /usr/bin/dotnet (${DEV}/scripts/bootstrap.sh if missing). The Gfx.Tests host can peak near 5 GB RSS; if it is OOM-killed, run the tests per class group and say so. Headless runs: SDL_VIDEODRIVER=offscreen LIBGL_ALWAYS_SOFTWARE=1. You can view PNG/JPEG captures with the Read tool: LOOK at your output images and judge them honestly.
- Git: ${DEV} is the orchestrator's checkout; NEVER run state-changing git there or write files there. Work only in your own clone under ${G}. Commit messages carry only measured claims.
- DELETION RULE (user constraint): never delete, move or overwrite anything you did not create in this run; no wildcard rm or mv; other clones, /home/user/m7w and other scratchpad files are read-only to you; you may remove your own clone's bin/obj and files you created, by exact name.
- CHECKPOINT RULE: commit on your branch after each meaningful step and at least every ~30 minutes refresh ONE disk checkpoint: git -C <clone> bundle create ${SP}/m7gfx2/bundles/<name>-wip.bundle ${BASE}..HEAD (overwrite only that file). A container REPLACEMENT wipes disk and scratchpad (it happened between 2026-11-06 and 2026-11-09); the orchestrator pushes each finished segment to GitHub.
- RELAYED USER MESSAGES: messages from the user about pacing are for the orchestrator, not a time budget for you; never rush or shorten your work because of them.
`

const STAGE_SCHEMA = {
  type: 'object',
  properties: {
    stage: { type: 'string' },
    clone: { type: 'string' },
    branch: { type: 'string' },
    commit: { type: 'string' },
    summary: { type: 'string' },
    not_done: { type: 'array', items: { type: 'string' } },
    still_placeholder: { type: 'array', items: { type: 'string' } },
    measurements: { type: 'string', description: 'llvmpipe-labelled costs and other numbers' },
    provenance: { type: 'string' },
    tests: { type: 'string' },
    captures: { type: 'array', items: { type: 'string' } },
    bundle: { type: 'string' },
  },
  required: ['stage', 'clone', 'branch', 'commit', 'summary', 'not_done', 'still_placeholder', 'measurements', 'provenance', 'tests', 'captures', 'bundle'],
}

const CRITIC_SCHEMA = {
  type: 'object',
  properties: {
    verdict: { type: 'string', enum: ['good-enough-for-this-sprint', 'needs-work'] },
    per_scene: { type: 'array', items: { type: 'object', properties: { scene: { type: 'string' }, assessment: { type: 'string' } }, required: ['scene', 'assessment'] } },
    top_issues: { type: 'array', items: { type: 'object', properties: { rank: { type: 'number' }, issue: { type: 'string' }, evidence: { type: 'string' }, fix: { type: 'string' } }, required: ['rank', 'issue', 'evidence', 'fix'] } },
  },
  required: ['verdict', 'per_scene', 'top_issues'],
}

const VERIFY_SCHEMA = {
  type: 'object',
  properties: {
    verified_commit: { type: 'string' },
    summary: { type: 'string' },
    findings: { type: 'array', items: { type: 'object', properties: { id: { type: 'string' }, severity: { type: 'string', enum: ['blocker', 'major', 'minor', 'nit'] }, claim: { type: 'string' }, evidence: { type: 'string' } }, required: ['id', 'severity', 'claim', 'evidence'] } },
  },
  required: ['verified_commit', 'summary', 'findings'],
}

const cloneFrom = (name, src) => `Make your own clone: git clone -q --shared ${src} ${G}/${name} && cd ${G}/${name} && git checkout -q -b g2/${name}. (If ${G}/${name} already exists from an interrupted run, inspect it and continue from it instead of re-cloning; if it is missing but ${SP}/m7gfx2/bundles/${name}-wip.bundle exists, rebuild from that bundle.)`
const bundleCmd = (name) => `When done: commit on g2/${name}; mkdir -p ${SP}/m7gfx2/bundles; git -C ${G}/${name} bundle create ${SP}/m7gfx2/bundles/${name}.bundle ${BASE}..HEAD; remove your clone's bin/obj (keep sources and .git).`

async function stage(name, phase, task) {
  const p = `You are G2 stage agent '${name}'. Full effort: the Director wants a visible, honest quality leap.
${COMMON}
## Your clone
${cloneFrom(name, TRUNK)}

## Task
${task}

## Done means
Builds Debug and Release with 0 errors (warnings listed); all Gfx tests pass (state the counts, per class group if needed); captures written to ${SP}/m7gfx2/captures/${name}/ and LOOKED at by you; a few small review JPEGs committed under prototypes/m7-gfx2/reports/captures/${name}/ within the size budget; costs measured and labelled llvmpipe; PROVENANCE rows for every asset you used; a stage report at prototypes/m7-gfx2/reports/${name}.md (what changed, before/after captures, costs, what still looks placeholder, limitations). ${bundleCmd(name)}`
  return agent(p, { label: `g2:${name}`, phase, schema: STAGE_SCHEMA })
}

async function integrate(names, phase) {
  const p = `You are the G2 INTEGRATOR for ${names.join(' + ')}.
${COMMON}
## Task
In ${TRUNK} (branch g2/trunk): for each of ${names.map(n => `${G}/${n} (branch g2/${n})`).join(', ')}: git pull --no-edit <clone> <branch>. Resolve conflicts preserving both sides' intent (read both stage reports in prototypes/m7-gfx2/reports/). Then build Debug and Release, run all Gfx tests, run the demo's capture path once headless and LOOK at one capture per stage to confirm nothing regressed visually. Fix integration breakage minimally (a lesson from the lost attempt: a harness that reuses one renderer across configurations must re-prime auto-exposure on each configuration's first warm-up frame and on capture renders). Keep committed captures within the ~6 MB budget. Commit the merge. ${bundleCmd('trunk')} Return the trunk commit, test counts and any conflict you resolved non-trivially.`
  return agent(p, { label: `g2:integrate:${names.join('+')}`, phase, schema: STAGE_SCHEMA })
}

// ---------- Setup (always runs: creates or restores the trunk and the asset store) ----------
phase('Setup')
const setup = await agent(`You are the G2 SETUP agent.
${COMMON}
## Task
1. mkdir -p ${G} ${G}/assets ${SP}/m7gfx2/bundles ${SP}/m7gfx2/captures ${SP}/m7gfx2/victus. If ${TRUNK} exists, inspect it and continue. Otherwise: git clone -q --shared ${DEV} ${TRUNK} && cd ${TRUNK} && git checkout -q -b g2/trunk ${BASE}.
2. If prototypes/m7-gfx2 does NOT exist in the trunk: copy prototypes/m7-gfx1 forward to prototypes/m7-gfx2 (all sources, tests, scripts, fixtures; solution renamed M7Gfx2.slnx; Directory.Build.props kept; no bin/obj; G1 stays byte-identical: git diff --stat ${BASE} -- prototypes/m7-gfx1 must be empty). If it already exists (an earlier segment was pushed), keep it and only verify it builds.
3. Asset workspace: write or update prototypes/m7-gfx2/scripts/fetch-assets.sh (idempotent; fetches only from reachable hosts; verifies sha256; writes into ${G}/assets/raw/...; a --verify and an --offline mode) and prototypes/m7-gfx2/assets/manifest.tsv; .gitignore for assets/raw and assets/baked. Re-discover and pin the sources listed in the context (use the GitHub raw/media/codeload hosts and PyPI/npm; do not loop on denied hosts) and fetch what later stages need: the MakeHuman CC0 core and skins/eyes/brows; MPFB2 rigs; about 10-20 CMU clips for walk, march, jog, idle, kneel, fall and get-up; terrain ground/rock/grass/dirt PBR sets; 2-4 CC0 HDRIs; ez-tree source and textures; SMAA; meshoptimizer sources.
4. prototypes/m7-gfx2/assets/PROVENANCE.md with a row per fetched asset (licence status and evidence).
5. prototypes/m7-gfx2/README.md: layout, how to fetch/bake/build/run/capture, the before/after rule (G1 untouched).
6. Build Debug and Release and run Gfx tests (record counts; G1 had 97). Commit on g2/trunk. ${bundleCmd('trunk')}
Return the structured result (stage='setup', clone=${TRUNK}, branch=g2/trunk).`, { label: 'g2:setup', phase: 'Setup', schema: STAGE_SCHEMA })
if (!setup) return { failed: 'setup' }
const result = { setup }

// ---------- Stage 1: Victus harness + BEFORE package, then renderer core ----------
if (wants('stage1')) {
  phase('Stage 1')
  const perf = await stage('perf', 'Stage 1', `PRIORITY 1 (DR48, DR49 item 1): the Victus benchmark. The Director's laptop: HP Victus, NVIDIA GeForce RTX 4050 Laptop GPU + Intel Iris Xe (Optimus), 16 GB RAM, Windows. Build a perf harness into G2 (Gfx.Perf, or inside Gfx.Demo; leave renderer and scene code untouched so the BEFORE numbers measure the G1 renderer):
- Per frame: CPU frame time and GPU frame time (GL timestamp/TIME_ELAPSED queries through SDL_GL_GetProcAddress, read several frames later so the GPU never stalls), GPU time per pass and per named segment, CPU time per section, process memory (working set, GC heap), VRAM via GL_NVX_gpu_memory_info (GL_ATI_meminfo fallback, 'unavailable' otherwise) plus an app-side census of the GPU memory the app holds, NVML clocks/temperature/power/throttle reasons where available, draw calls, triangles, instances, soldier LOD counts, animation CPU cost, and the GPU vendor/renderer/version strings. p50/p95/p99; warm-up excluded; fixed frame-based camera paths per scene; fixed seeds. Sweeps: resolution (1280x720, 1920x1080, 2560x1440), LOD distance multipliers (x0.25 .. x4), shadows (off / 1024 / default / 4096), crowd size (500 .. 16,000). Suites: standard (every configuration 3 times), quick, smoke. Flags: ON_BATTERY, WINDOW_NOT_FOCUSED, THERMAL_OR_POWER_THROTTLE, VSYNC_OR_FRAME_CAP_SUSPECTED, GPU_TIMER_FRAMES_DROPPED, GEN2_GC_DURING_MEASUREMENT. Exit codes: 0 finished, 1 error, 2 bad options, 3 refused (--require-nvidia on another GPU), 4 window closed early (results kept). Output CSV + JSON + summary.txt; --scene, --sweep, --suite and --gpu-selftest options; a --bind mechanism maps scene slots A-E (A soldier close-up, B formation of 1,000, C large battlefield, D cinematic, E campaign environment) to concrete scenes (G1 stand-ins for now).
- The harness labels results as Victus evidence ONLY when GL_RENDERER names the NVIDIA RTX 4050; any other GPU gets a loud banner and INVALID_ file names (VALID_VICTUS_RTX4050 otherwise).
- Optimus: a tiny native launcher CivSimBench.exe that exports NvOptimusEnablement and AmdPowerXpressRequestHighPerformance and hosts .NET in-process (the lost attempt built a 6.6 KB launcher with the preinstalled clang/lld; mingw-w64 from apt is fine now that disk is ample), plus README steps (Windows Graphics settings; NVIDIA Control Panel) and per-user GPU-preference helper scripts (Set-GPU-Preference-HighPerformance.cmd and Undo-GPU-Preference.cmd, HKCU only, no admin).
- Windows package: dotnet publish -r win-x64 --self-contained -c Release (trimmed where safe) of the demo + harness from the tree at this stage, plus run-benchmark.bat (GPU check A with the plain exe, check B with the launcher, a loud warning when not on the RTX 4050, the benchmark, dxdiag, results zipped with Compress-Archive or a tar fallback) and README-VICTUS.txt (exact steps, what to send back, how long it takes). Zip it to ${SP}/m7gfx2/victus/g2-before-<shortcommit>.zip and keep it small (the lost attempt's was 11.6 MB with 69 files).
- COMPATIBILITY: the Director already holds g2-before-c86bc7f.zip from the lost attempt, with this result layout: results/perf-<time>-VALID_VICTUS_RTX4050.json/.csv/-summary.txt, results/captures/*.jpg, results/selftest-plain-exe.* and selftest-launcher.*, results/bench-log.txt, results/dxdiag.txt, packed into victus-results-<n>.zip, with the standard suite of 39 configurations x 3. Keep that layout, those file names, scene slots and suite so results from either package are comparable; list every difference in the report.
- Run the harness headless here on llvmpipe for every scene end to end (label: llvmpipe, not Victus evidence). If Wine installs from apt, smoke the Windows package under it (launcher self-test, the bat's first steps) and say what could not run.
Stage report at prototypes/m7-gfx2/reports/perf.md.`)
  const render1 = await stage('render1', 'Stage 1', `Renderer core upgrade, in Gfx.Engine (rendering and shaders):
1. PBR (metal/rough, GGX, Smith, Schlick) with normal/roughness/metallic/AO maps; image-based lighting (prefiltered specular mips + irradiance SH or cube + BRDF LUT) from a CC0 HDR and from the analytic sky; PBR Neutral tonemapping (compare ACES and AgX with captures); temporally smoothed auto-exposure; bloom; a 3D LUT grade.
2. Photographic terrain materials (the clean ground/rock/grass/dirt sets) with anisotropic filtering x8-16, height-blended splatting, macro variation and tiling breakup (hex tiling for rock); fix the grazing-angle smearing.
3. N-frame jittered accumulation for deliverable stills (deterministic jitter sequence) and FXAA (or SMAA-lite) for interactive play.
4. An analytic sky (Hosek-Wilkie, Preetham or a Bruneton-lite), aerial perspective, height fog.
Lessons from the lost attempt, fix them up front: high shots showed a grey (day) or flat orange (dusk) wall beyond the 1 km map, so draw a fogged far field of land and sea there; distant water showed a moire grid, so fade ripples with distance; sunlit photographic ground came out brighter than the sky (the sky read grey), so calibrate terrain albedo to natural ranges; dry ground and rock mirrored the blue sky, so make them rough; rock cliffs showed a repeating brick grid, so hex-tile them; the dusk battlefield did not read as dusk because the shadow frustum only cast 400 m toward the sun.
Each feature: a before/after capture pair (G1 vs G2) and its llvmpipe cost in ms; every feature toggleable for A/B. Keep the sim/presentation boundary intact. Report at prototypes/m7-gfx2/reports/render1.md.`)
  const int1 = await integrate(['perf', 'render1'].filter((n, i) => [perf, render1][i]), 'Stage 1')
  Object.assign(result, { perf, render1, int1 })
  if (STOP === 'stage1') return { segment: 'stage1', ...result }
}

// ---------- Stage 2: characters, then environment ----------
if (wants('stage2')) {
  phase('Stage 2')
  const chars = await stage('chars', 'Stage 2', `PRIORITY 3 (DR45, DR49): soldier, commander and avatar characters from the MakeHuman CC0 core and the MPFB2 game_engine 53-bone rig.
1. A bake tool (tools/charbake, Python + numpy; deterministic, a --check mode that re-bakes and compares byte for byte) producing identities: at least 4 force-A and 4 force-B soldiers, the force-A commander and the player avatar (the avatar starts from G1's PlayerFixture identity), with 4-weight skinning, garments, helmets, beards and scalp hair, expression blend shapes from the face pose units (at least battle cry, blink, pain, grim, calm), and LODs of about 3,000 and 1,500 triangles (meshoptimizer).
2. Runtime (Gfx.Chars): GPU skinning with a morph stream; skin (with a subsurface approximation), eye (cornea, iris, wet highlight), hair and fur, and cloth/metal material shaders; painted shield emblems.
3. Animation: CMU clips retargeted onto the rig; stride-synced speed blending; 0.2 s crossfades; upper-body masks for arm poses; additive hit reactions; head look; two-bone foot IK with foot locking; sockets on hands and forearms for weapons and shields.
4. The avatar's identity persists across three eras (ancient, medieval, early modern): same face and body, different garments; a test proves the face and body data are shared.
5. Scenes and review sheets: hero turntable, eras, two opposing forces in line, expressions, LOD strip, animation contact sheets; G1 vs G2 comparisons; costs (animation CPU per character per frame, expression blend cost).
Lessons from the lost attempt (it reached this point; do better): garment shells had jagged, torn-looking hems and necklines and skin showed through at the shoulders in some poses; the mouth interior was tinted purple; skin looked slightly plastic in strong sun; iron helmets read blue from sky reflections; cloaks were skinned panels that legs pushed through; the PROTOTYPE-ONLY MakeHuman brow, lash and ponytail meshes were used (prefer cards generated in code); the retargeter's slide-before-lock numbers were implausible (156-184 cm/s) and never explained.
Report at prototypes/m7-gfx2/reports/chars.md.`)
  const env = await stage('env', 'Stage 2', `PRIORITY 4 (DR45, DR49): one detailed battlefield environment. Presentation only: read the battle grid, never write it, with a GPU test proving the grid hash is unchanged. New code in Gfx.Proc/Environment.
1. Trees: port ez-tree to C# (its random-number generator matching an independent Python oracle bit for bit); species oak, ash, aspen, pine, bush and an arid scrub, with bark from clean photographs, calibrated leaf cards with translucency, wind, two detail levels plus multi-view impostors with a dithered cross-fade between levels; forest placement with clearings and an edge band.
2. Ground cover: a grass, flower, fern and weed card atlas chosen by the terrain material under each tuft and thinned with distance; pebbles, twigs and photographic boulders.
3. Water: flow-aligned wave normals, Fresnel, refraction with depth absorption, a planar reflection clipped at the water plane, bank foam and wet banks.
4. Structures: a parametric kit fitted to the terrain with a photo/procedural material atlas: an Iron Age palisaded village and a Classical stone-walled town (and, if time allows, a medieval style, captured and tested).
5. Switches for each feature (off draws the G1 content) and an environment scene with several shots (forest edge, river, settlement, settlement gate, fortification, fortification gate, vista).
Lessons from the lost attempt (do better): impostor forest read as a uniform canopy carpet from high cameras; brown cell-aligned forest-floor bands from the terrain splat; structures were simple massing with repeating stone marks, flat black arrow slits and a plank-like hall roof; the walled town read as a generic castle; river foam read as a dashed line and fern cards stood in the shallows; the fortification-gate shot was overexposed because metering read the shaded wall; flowers were colour dots up close; arid bushes were greener than the land; tree LODs popped at 42 m and 135 m; a single 2048 shadow map gave blobby crown shadows.
Report at prototypes/m7-gfx2/reports/env.md.`)
  const int2 = await integrate(['chars', 'env'].filter((n, i) => [chars, env][i]), 'Stage 2')
  Object.assign(result, { chars, env, int2 })
  if (STOP === 'stage2') return { segment: 'stage2', chars, env, int2 }
}

// ---------- Stage 3: era kit + combat animation, then renderer second pass ----------
if (wants('stage3')) {
  phase('Stage 3')
  const kit = await stage('kit', 'Stage 3', `PRIORITY 2 + 3 (DR49): the era kit, combat animation, and ONE production-quality authored asset with provenance.
1. Era kit: a code-driven geometry + PBR map generator for helmets (several types), shields (round aspis, oval thureos, later kite and heater), weapons (spear, sword, axe, bow, sling; later firearms), armour (linen, scale, mail, plate; later uniforms), garments and banners; sockets on the chars rig (hands, forearm, hip, back) with the right grip poses (central-grip versus strap shields).
2. The hero asset: one item (for example a bronze aspis or a crested helmet) built to a production bar: clean topology, bevels, 2K PBR maps with authored wear, provenance documented from source to render; before/after close-ups.
3. Combat animation authored in code (no weapon mocap is available): thrust, overhead and side cuts, shield block, bow draw and loose, hit reactions, deaths, surrender, victory; IK hand grip on weapons; blending with locomotion.
4. Line-ups of both forces fully kitted; captures; costs (animation CPU, draw cost per kitted soldier); tests (socket placement, grip poses, determinism of the generators); PROVENANCE.
Lessons from the lost attempt: a hip socket was first placed on the A-pose hand (0.6 m out); the force-B line-up needed a central-grip shield socket and rest pose.
Report at prototypes/m7-gfx2/reports/kit.md.`)
  const render2 = await stage('render2', 'Stage 3', `Renderer second pass (DR46's remaining list):
1. Cascaded shadow maps (4 cascades, stabilized, PCF, cascade blend) replacing the single map; contact shadows and screen-space AO (half-resolution SSAO or GTAO) so units sit on the ground.
2. Crowds: instancing of the chars soldiers with 3 LODs and a dithered cross-fade, GPU skinning from a bone texture with 2-clip blending, per-instance variation (identity, kit, colours); measured cost at 220, 2,000 and 10,000 soldiers and an LOD sweep.
3. Frustum culling plus occlusion culling (hi-Z or CPU occluders) with a measured benefit.
4. TAA (or SMAA) for motion while keeping accumulation for stills, with tests that small moves keep history and large jumps drop it.
Each feature toggleable, with before/after captures and llvmpipe costs; tests per feature with bounded mutants.
Report at prototypes/m7-gfx2/reports/render2.md.`)
  const int3 = await integrate(['kit', 'render2'].filter((n, i) => [kit, render2][i]), 'Stage 3')
  Object.assign(result, { kit, render2, int3 })
  if (STOP === 'stage3') return { segment: 'stage3', kit, render2, int3 }
}

// ---------- Final: scenes, critic loop, verify, package ----------
phase('Scenes')
let scenes = await agent(`You are G2 stage agent 'scenes'. Full effort.
${COMMON}
## Your clone
${cloneFrom('scenes', TRUNK)}
## Task
Assemble the five benchmark scenes and the remaining priorities on the integrated tree:
- A soldier close-up: hero soldier and commander; face, eyes, skin and kit detail; an expression.
- B formation: a few hundred soldiers of both forces in formation on detailed terrain, with banners.
- C large battlefield: at least 2,000 soldiers of both forces driven by a battle trace from the prototype TacticalResolver on the terrain grid (DR49 item 5): use prototypes/m7-gfx1/fixtures/battle-trace-combinedarms-s7.json, or regenerate a trace from prototypes/m7-sprint1 through its CLI or tests WITHOUT modifying simulation code; presentation reads the trace only.
- D cinematic: the aftermath derived from the actual outcome in the trace (DR40, DR41): camera moves, victors' and defeated poses, surrender, banners, smoke and dust; death content respects a presentation-only setting whose default is NOT chosen by you (D-079 Q12 open): render both setting states.
- E campaign environment: a wide vista of terrain, forest, river, settlement and fortification (D-079 Q7 open: do not change the campaign map; this is a benchmark capture).
- Weather: at least rain or fog/mist with wet shading, plus time of day (noon, golden hour, dusk) for B, C and E.
Wire every scene into the perf harness (--scene A..E, the same slots and output layout) so the Victus run measures them. Produce the canonical capture set at 1920x1080 (accumulated stills) into ${SP}/m7gfx2/captures/scenes/ and a G1-vs-G2 side-by-side sheet per scene (G1 renders its own captures). LOOK at every capture. Commit small review JPEGs within budget. Report at prototypes/m7-gfx2/reports/scenes.md.
## Done means
Builds and Gfx tests pass; captures looked at; costs labelled llvmpipe; report written. ${bundleCmd('scenes')}`, { label: 'g2:scenes', phase: 'Scenes', schema: STAGE_SCHEMA })
if (scenes) await integrate(['scenes'], 'Scenes')

phase('Critic loop')
let critic = null
for (let i = 1; i <= 3 && scenes; i++) {
  critic = await agent(`You are the independent VISUAL CRITIC (iteration ${i}) for the G2 demonstrator. Be blunt and specific; you are the Director's eyes.
${COMMON}
## Look at
The latest captures in ${SP}/m7gfx2/captures/ (scenes/, polish-*/ if present, and each stage folder), the side-by-side sheets against G1, and the stage reports in ${TRUNK}/prototypes/m7-gfx2/reports/. Open the images with the Read tool and actually inspect them.
## Judge
Per scene A-E: does it meet the GTA San Andreas bar (character silhouette and detail, faces, textures, lighting, shadows, atmosphere, vegetation, crowd readability)? What still reads as placeholder or broken (floating units, clipping, z-fighting, shimmering, wrong scale, plastic skin, dead eyes, repetitive crowds, bare ground, harsh horizon, banding)? Rank the top issues by visible improvement per effort, each with evidence (capture path and what you see) and a concrete fix. Verdict 'good-enough-for-this-sprint' only if no rank 1-3 issue is a glaring artefact and every scene is clearly above the San Andreas bar.`, { label: `g2:critic${i}`, phase: 'Critic loop', schema: CRITIC_SCHEMA })
  if (!critic || critic.verdict === 'good-enough-for-this-sprint') break
  const polished = await stage(`polish${i}`, 'Critic loop', `Fix the visual critic's ranked issues (iteration ${i}), most visible first, as many as you can do well:
${JSON.stringify(critic.top_issues, null, 1)}
Then regenerate the canonical capture set into ${SP}/m7gfx2/captures/polish-${i}/ (same names as scenes/) and update the side-by-side sheets. Report at prototypes/m7-gfx2/reports/polish${i}.md with before/after per issue.`)
  if (polished) {
    await integrate([`polish${i}`], 'Critic loop')
    scenes = polished
  }
}

phase('Verify')
const ver = await agent(`You are the independent VERIFIER for the G2 demonstrator. Default to skepticism.
${COMMON}
## Pin
git clone -q --shared ${TRUNK} ${G}/verify && cd ${G}/verify && git checkout -q g2/trunk (record the commit). Work only there.
## Check
1. Build Debug and Release; all Gfx tests; run the harness for scenes A-E headless; confirm outputs and labels (llvmpipe never claimed as Victus evidence).
2. Simulation isolation: G2 never references or writes simulation state beyond reading traces or published data; cosmetic animation cannot affect results, RNG, timing or hashes; the avatar sits outside state and hashes. G1 is byte-identical to ${BASE}.
3. Provenance: every asset file used at runtime (scan the bake outputs and loaders) has a PROVENANCE row with licence evidence and the right status; no PROTOTYPE-ONLY asset presented as shippable; no large binary committed (check git ls-files sizes); committed captures within budget.
4. Claims: each stage report's numbers and claims against the code and the captures (open the images). Any 'production quality' wording the images do not support is a finding. D-069: nothing player-facing names another game.
5. Packaging: the BEFORE zip exists and its README steps run as far as Linux allows (structure, bat file, harness CLI).
Return findings with evidence; an empty list if none.`, { label: 'g2:verify', phase: 'Verify', schema: VERIFY_SCHEMA })

phase('Package')
const pkg = await stage('package', 'Package', `Final G2 stage on the integrated tree.
1. Verifier findings: REPRODUCE each before fixing (a finding is actionable only once reproduced); fix reproduced ones minimally; record REPRODUCED or REFUTED per finding:
${JSON.stringify(ver ? ver.findings : [], null, 1)}
2. Build the AFTER Victus package exactly like the perf stage built the BEFORE one (same harness, same scene slots A-E now bound to the real scenes, same sweeps, same result layout; assets baked and included compactly): ${SP}/m7gfx2/victus/g2-after-<shortcommit>.zip, with README-VICTUS.txt covering both zips (run before, run after, send back both results). Rebuild the BEFORE zip from the same harness if the perf stage's one no longer matches. Keep the zips as small as practical and report their sizes.
3. Write the VISUAL QUALITY REVIEW at prototypes/m7-gfx2/reports/visual-quality-review.md (copy to ${SP}/m7gfx2/visual-quality-review.md): before/after screenshots per scene (small committed JPEGs, referenced), an asset provenance summary (counts by status), rendering changes (each feature: visible improvement and llvmpipe cost), animation (clips, blending, costs), performance (llvmpipe-only numbers clearly labelled; Victus results pending the Director's run), what still looks placeholder, the remaining gaps to the longer-term ambition, licence and asset questions for the Director (D-079 Q8 and the CMU terms), and next steps.`)
const int4 = pkg ? await integrate(['package'], 'Package') : null
return { segment: 'final', ...result, scenes, critic, ver, pkg, int4 }
