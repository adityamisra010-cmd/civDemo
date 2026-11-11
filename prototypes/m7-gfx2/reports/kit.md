# G2 stage kit: era kit, hero asset, combat animation

Stage agent kit, branch `g2/kit` (cut from `g2/trunk` 7dd134a), 2026-11-11. Scope: DR49 items 2 and 3, the era kit
generated in code (geometry and PBR maps), sockets and grip poses on the stage-chars rig, one hero asset to the
production bar, combat animation authored in code, both forces fully kitted. Every number below was measured in this
container on **llvmpipe (Mesa 25.2.8, LLVM 20.1.2, 4 logical CPUs): llvmpipe, not Victus evidence**. No hardware GPU
ran this code, and no number here says anything about the Victus.

**Verdict, honestly.** Before this stage the soldiers carried the kit the character bake made for them: flat
painted shield discs, plain spears and swords, helmets as shells of the head. Now every helmet, shield and weapon is a generated item with its own
geometry and its own 512 to 2,048-texel base colour, normal and ORM maps, fitted to the wearer's head or hand: five
helmets, five shield kinds, eleven weapons and two standards, on two ancient forces (hoplites against Celtic
warriors) and on the stage-chars medieval soldiers. Spears are held overhand in a closed fist, strap shields sit on
the forearm with the fist on the hand grip, central-grip shields turn about their bar, and 37 combat clips authored
as keys and solved by IK (thrust, two cuts, block, bow draw and loose, hit, two deaths, surrender, victory) replace
the generic motion capture for every action. **The hero asset, the bronze-faced aspis, is the one item I would put
at the production bar** (§2): clean lathe and sweep topology with rolled edges, three LODs, 2,048-texel maps with
authored wear, a deterministic glTF bake with provenance from source to render. **Everything else is better than
before but still placeholder in places** (§7): the other items are prototype quality (procedural materials that read
well at line distance, thin on close-up detail), the combat motion is stiff key-to-key motion without secondary
motion, there is no cloth simulation, and the per-soldier draw cost is far too high for crowds (§5).

## 1. What now exists

| Part | Where | What it does |
|---|---|---|
| Geometry | `Gfx.Gear/Geometry/` | `KitMesh` (positions, normals, glTF-convention tangents, UVs, a per-vertex mask colour the painters read, triangles; double-precision build, float storage). `Shapes`: lathe, sweep along rotation-minimising frames, loft of cross-sections, grids, tubes, fillets, rounded rectangles, marching-triangle cuts by a field, solidify with rolled edges, auto-smooth normal splitting |
| Maps | `Gfx.Gear/Maps/` | `MapSet`: base colour (sRGB), tangent-space normal (green up) and ORM (glTF packing) painted per texel, either analytically over a UV rectangle or by rasterising a mesh in UV space; heights become normals scaled by each texel's world size; cavity occlusion; dilation; mips with Toksvig-style roughness widening. `Noise` (integer-hash value, Perlin, fBm, ridged, Worley; no `System.Random`), `Materials` (bronze, iron and steel, wood, leather, cloth, paint with chips, scratches, dust) |
| Items | `Gfx.Gear/Items/` | `Aspis` (the hero), `Shields` (thureos, round, kite, heater), `Helmets` (Corinthian with horsehair crest, Montefortino, nasal, kettle hat, great helm; fitted to the measured head), `Weapons` (spear, xiphos, La Tène long sword, arming sword, axe, bow, arrow, quiver, scabbard, sling, matchlock musket), `Banner` (standard), `Stands` (display post and rack). Seeds vary wear, paint and proportions; devices are original geometric designs |
| Rig | `Gfx.Gear/Rig/` | `KitSockets`: grips (from the bake), forearms (porpax and enarmes position), hips (at the measured hip width), back, head; `BodyMeasure` from the identity's body. `Grip`: finger wrap onto a cylinder of the grip's radius. `KitHold`: the pose constraint (strap, central grip, two-handed, item in hand) and the items' world transforms |
| Runtime | `Gfx.Gear/Runtime/` | The kit material (`KitEffects`: GPU-skinned like every character part, three maps, the stage-chars surface model with GGX sun, cascaded shadows, image-based light, cloth sheen, hashed alpha for fringes and fletching), map uploads with CPU mips, props and worn kit as character-file assets, helmets hiding the hair under them, `KitSoldier` (body + loadout + hold + driver, dropped items falling to lie flat), `BowRig` (limbs bend so the string reaches the drawing hand) |
| Animation | `Gfx.Gear/Anim/` | `Authoring`: keys (pelvis, spine and head angles; hand grip frames; foot targets; knee and arm angles), eased per segment, hand paths through Catmull-Rom, baked per skeleton through two-bone IK into 30 fps clips, clamped above the ground. `CombatClips`: 37 clips per skeleton |
| Hero bake | `Gfx.Gear/Bake/`, `Gfx.Demo --kit-bake`, `scripts/bake-kit.sh` | glTF 2.0 binary writer and reader, own PNG encoder and decoder, the aspis with three LODs and embedded 2,048-texel maps, MANIFEST and provenance JSON; `--check` re-bakes in memory and compares byte for byte |
| Outfits | `tools/charbake/kitbake.py`, `recipes.soldier_ancient`, `scripts/bake-kit-outfits.sh` | Garments of the kitted forces (force A: linen chiton, linen corselet or bronze cuirass, pteruges, greaves on some; force B: wool tunic, trousers, belt, mail on one), built like stage-chars outfits, without helmets, shields or weapons |
| Hooks in `Gfx.Chars` | 41 lines added, 6 removed (`git diff --numstat 7dd134a..HEAD`) | A material extension point (`CharMaterials.Extension`), the bone palette sized by the largest skeleton drawn, per-instance bounds, a pose-constraint hook in the animator (`AnimInput.Constraint`, run after foot IK) and read access to its solved globals. Nothing else in stage chars changed |
| Scenes | `Gfx.Demo/Scenes/KitScenes.cs` | `kit-hero`, `kit-gallery`, `kit-forces`, `kit-combat`, `kit-medieval` on the stage-chars meadow |
| Costs | `Gfx.Demo --kit-costs` (`Gfx.Demo/KitCosts.cs`) | §5 |
| Tests | `Gfx.Tests/KitTests.cs` | 18 tests (§6) |
| Review sheets | `scripts/review-sheets-kit.py` | `reports/captures/kit/` (§4) |

DR42 holds by construction: `Gfx.Gear` references `Gfx.Engine` and `Gfx.Chars` only, and a test fails if it ever
references an assembly whose name starts with `Sim.` or `Proto`. Nothing reads or writes simulation state; the
soldiers are scripted per scene.

## 2. The hero asset: bronze-faced aspis

**Form** (Greek hoplite shield, about 500–350 BC; form only, no game asset or likeness): a deep wooden bowl about 0.92 m
across with an offset flat rim, a bronze facing turned over the rim, a central porpax arm band with palmette-ended
plates riveted to the inside, an antilabe cord hand grip near the rim, and a carrying cord running in festoons from
bronze staples round the inner rim. The front carries a painted sun device on a bronze field; the rim a guilloche
band between beads.

**Topology.** Lathe and sweep surfaces; the profile is filleted where the bowl's wall meets the rim face (3 mm, then
a half-round bead) and where the back plane meets the inner wall (6 mm); fittings are solidified with rolled edges; normals are
split only where faces meet at more than 38 degrees (45 at the lower LODs). LOD0 48,156 triangles and 33,131 vertices; LOD1 8,456 / 5,884; LOD2 1,736 / 1,073 (rebuilt by the generator at lower segment counts, sharing the UV
layout and maps).

**Maps.** One 2,048 × 2,048 atlas set: base colour, normal, occlusion-roughness-metallic. Authored wear, all
deterministic in the seed: planishing facets (Worley cells about 9 mm) and polishing rings on the bronze, dents with
raised rims, tarnish in hollows and verdigris where it stays damp, polish on the high points and the rim, the painted
device chipped to the bronze along its edges and scratched across, the rim's guilloche struck as relief, the poplar
core inside carved with adze facets along the grain and darkened by grime where the arm and hand rub, the flax cord
in two plies.

**Bake and provenance chain.** `scripts/bake-kit.sh` (Release `Gfx.Demo --kit-bake`) writes
`$G2_ASSETS/baked/kit/hero/aspis.glb`, `MANIFEST.tsv` (sha256 of the file and of each embedded PNG) and
`aspis.provenance.json` (generator, parameters, commit, triangle counts, timings). Current file: commit `8df7e55`,
7,169,712 bytes, sha256 `18474bcad1abd847e2047257cf7ad2f3b51805ed8d089bb7c5d72f3b00f81928`; `--check` re-bakes in
memory and compared it byte for byte: identical. `kit-hero` and `kit-forces` (the commander's shield) load the glTF
file through the same reader the test uses, so what is rendered is the file whose hash is recorded. Source → render:
`Gfx.Gear/Items/Aspis.cs` (parameters `HeroBake.Spec`) → `aspis.glb` → `HeroBake.Load` → kit material → the frame.
`assets/PROVENANCE.md` §3.11 has the row.

**Before/after.** `kit-01-hero-before-after.jpg` puts the stage-chars bake's aspis (BEFORE) beside the hero (AFTER)
under the same cameras, standing on its rim and held by the avatar; `kit-02-hero-closeups.jpg` has the rim, the device,
the fittings and the late-sun view, with the camera about 0.6 m from the surface. The before shield is a smooth disc
with a flat device and no fittings worth a close-up.

**Fixed during the run by looking at the captures:** the inner wood showed flat polygons, because each adze facet got
its own albedo step; the facets now shade continuously towards their rims (re-baked at `8df7e55`).

## 3. Era kit, sockets and grips

Generated items (triangles at LOD0, map size; from `--kit-costs`, §5):

| Item | Triangles | Maps | Notes |
|---|---:|---:|---|
| aspis (hero; the line aspides use the same generator at 1,024) | 48,156 | 2,048 | §2 |
| thureos | 23,540 | 1,024 | oval, wooden spine with an iron strip boss, felt-covered planks, central grip |
| round shield | 14,716 | 1,024 | planks, iron dome boss, central grip |
| kite shield | 21,548 | 1,024 | curved, leather-covered, painted, enarmes and guige |
| heater shield | 17,360 | 1,024 | as the kite, later form |
| Corinthian helmet | 29,268 | 1,024 | bronze, eye openings and nasal cut by one field, horsehair crest on cards |
| Montefortino | 31,652 | 1,024 | bronze bowl, top knob, cheek plates |
| nasal helmet | 31,340 | 1,024 | iron cone, riveted bands, nose guard |
| kettle hat | 29,204 | 1,024 | iron bowl, wide brim |
| great helm | 32,816 | 1,024 | flat top, vision slits, breaths |
| spear, xiphos, long sword, arming sword, axe | 2,952–3,276 | 512 | lofted blades with midrib or fuller and bevelled edges, sockets, guards, grips, pommels |
| bow, arrow, quiver, scabbard, sling, musket | 116–2,152 | 512 | the bow is rigged (§1) |
| standard | 3,368 | 1,024 | wool cloth on a crossbar, painted device, fringe; cloth on a five-bone chain |

**Sockets** (`KitSockets`, test `SocketsSitOnTheBodyNotOnTheHands`): the lesson from the brief is built in. The hip
sockets are on the pelvis bone, outside the measured hip width at the hip line (within 0.20 m of the hip joint and
more than 0.25 m from the hand in every identity); the earlier version that hung a scabbard from the A-pose hand,
0.6 m out, cannot come back without failing the test. The forearm sockets sit on the back of the forearm 3–12 cm
from the elbow, the back socket behind the chest, the head socket within 12 cm of the head bone.

**Grip poses.** Right hand: the fist closes round the weapon's grip radius (spear shaft 15 mm, sword grips, the bow's
handle); each finger joint curls until its phalanx lies on the grip's cylinder (test: no joint inside the shaft, curl
shrinks as the grip thickens). Spears are held overhand (`grip_over`) for the hoplite guard and thrust. Left hand,
by the shield's kind:
- **Strap (aspis, kite, heater):** the shield is fixed to the forearm socket; `FitStrap` solves the wrist rotation and
  the slide along the forearm that put the fist on the hand grip (residual under 30 mm for the aspis and the kite in
  every identity; test `StrapShieldFitBringsTheFistToTheHandGrip`); a 28 mm stand-off for mail over a gambeson keeps
  the kite shields of `kit-medieval` off the mailed arm.
- **Central grip (thureos, round):** the fist closes on the bar behind the boss, and the board turns about the bar to
  face the way the soldier faces (or hangs by its weight when no direction is given; test
  `CentralGripShieldTurnsAboutItsBarToFaceTheWantedWay`). Force B has its own clips for this grip (guard, thrust,
  block, hit, deaths, surrender, victory: the `.c` set), whose left hand holds the bar in front of the body instead of
  the strap pose on the forearm.
- **Two-handed and in hand:** the off hand reaches the weapon's second grip by arm IK (`PlaceHand`); the bow is held
  in the left fist by its grip frame.

Worn kit (helmets, scabbards, quivers) is written in the socket's bind space and bound to its bone, so it moves with
the skeleton at no extra cost; a helmet hides the hair triangles under it but not the face (test
`HelmetsHideTheHairUnderThemButNotTheFace`).

## 4. Animation, scenes and captures

**Clips authored in code** (`CombatClips`), each for strap and central-grip shields where it applies: hoplite guard
(loop), overhand thrust over the shield rim with a step in, shield block (shield punched up, braced), heavy hit
(thrown back a step), backward death (struck, knees give, falls on the back, arms out; the shield slips off the arm
as he falls), forward death (doubles over, drops to the knees, falls forward, hands beside the thighs), surrender
(weapons dropped, kneels, open hands raised), victory (weapon raised, shaken twice); sword guard, overhead cut, side
cut, block, hit, deaths, surrender, victory; archer: bow raised, string drawn to the jaw, held, loosed, lowered.
Keys hold hand frames in character space; the baker solves arms and legs by two-bone IK, puts the feet on their
targets, keeps every joint at least 1 cm above the ground, and records foot contacts. The hold constraint then runs
on every animator step, so fists stay closed on the grips whatever the clip does.

**IK hand grip and blending with locomotion.** The constraint runs after the animator's foot IK, on the blended pose,
so it holds through crossfades, through the upper-body layer and on top of locomotion: the walking hoplite of
`kit-combat` walks on the stage-chars motion-capture walk with the authored guard on the upper body (masked layer),
spear and shield kept in hand by the constraint. Releases (dropping weapons when surrendering or dying) are functions
of scene time, so a still at time t is the same however it was reached; a dropped item falls for 0.45 s and lies flat.

**Scenes.** `kit-hero` (turntable in 45-degree steps, close-ups, the before shield beside it with the same cameras,
the avatar holding each), `kit-gallery` (shields, helmets on posts, weapons on a rack, close-ups), `kit-forces`
(force A: four hoplites and the commander with the hero aspis; force B: four Celtic or Galatian warriors with
thureoi and a round shield, one in mail, one bare-headed, a long sword with its scabbard; a standard each),
`kit-combat` (one performer per programme, side cameras), `kit-medieval` (the stage-chars medieval soldiers with the
generated great helm, nasal helmets, kettle hats, kite and heater shields, arming swords, an axe and a bow).

**Captures.** Full-size PNGs (1280 × 720) in the scratchpad:
`/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7gfx2/captures/kit/final2/`
(all 87 shots of the five scenes, single sample + FXAA, after the last fixes) and `…/captures/kit/s16/` (the 36
review shots as 16-sample jittered stills). I looked at every scene's shots, first in a complete pass at the
previous commit, then after these fixes: the swordsman's camera moved from the shield's face to his sword arm (and
inside the walking hoplite's path, who crossed it at 4.85 s), the archer's to his drawing side, the backward death's
to the side; the round shield's iron boss was half wood (its UVs overlapped the grip bar's texels); the aspis wood
(§2); the dead hoplite's shield now slips off his arm instead of standing on its rim beside him; the gallery has a
close-up of the three sword hilts instead of their blade tips. Review sheets in `reports/captures/kit/`:

| Sheet | Content | PROTOTYPE-ONLY in frame |
|---|---|---|
| `kit-01-hero-before-after.jpg` | stage-chars aspis vs hero aspis, same cameras; held by the avatar | CMU idle posing the avatar in the two "held" tiles |
| `kit-02-hero-closeups.jpg` | rim, device, fittings, late sun | none |
| `kit-03-gallery.jpg` | shields, backs, five helmets, three sword hilts, axe, spearhead | none |
| `kit-04-forces.jpg` | both lines, low view, force A, force B, commander, 17:36 sun | none (authored clips only) |
| `kit-05-combat.jpg` | thrust, block, hit, victory, overhead and side cuts, forward death, bow draw-hold-loose, surrender, backward death, walk with the guard layer | CMU walk in the two "walk" tiles |
| `kit-06-medieval.jpg` | medieval lines, both forces, three faces under the generated helmets | none (authored clips only) |

## 5. Costs (llvmpipe, not Victus evidence)

`Gfx.Demo --kit-costs`, Release, on the final tree; this container's 4 logical CPUs. The map painter shades texel
rows in parallel, so its times scale with the core count.

| Measure | llvmpipe, not Victus evidence |
|---|---|
| Geometry, all 23 generators (one of each kind above) | 587 ms in total: hero aspis 79 ms, shields 20–105 ms, helmets 40–64 ms, weapons 0.1–4 ms |
| Geometry + maps, all 23 | 9,949 ms in total: the hero's 2,048-texel set 2,157 ms; 1,024-texel sets 425–867 ms; 512-texel sets 54–203 ms |
| Hero glTF load (mesh + three 2,048-texel PNG decodes) | 304 ms; file 7,001 KiB |
| Map set upload with CPU mips (base, normal, ORM) | 203 ms at 2,048; 51 ms at 1,024 |
| Clip bake, every authored clip of one skeleton | 37 clips, 2,255 frames at 30 fps: 101 ms (45 µs per frame) |
| `kit-forces` load (nine outfits, nine kits generated with maps, nine clip bakes, uploads) | 15,024 ms |
| Animator step, authored action + hold constraint | mean 34.4 µs, p95 76.0 µs per soldier per frame (7,560 steps, 9 soldiers); the constraint alone mean 32.1 µs, p95 72.0 µs |
| Animator step, walk (CMU legs) + authored guard on the upper body + hold | mean 32.2 µs, p95 79.2 µs; the constraint mean 26.6 µs |
| Soldier update (guard step + hold, pose solve with skin matrices, item transforms) | mean 30.4 µs, p95 34.8 µs per soldier per frame |
| `kit-forces` `lines-34`, 1280 × 720, one frame | 685 draws, 5,398,555 triangles, 1,328 ms with the nine soldiers; 50 draws, 232,893 triangles, 617 ms without |
| Per kitted soldier (main and shadow passes) | 70.6 draws, 573,962 triangles, 79.0 ms of llvmpipe frame time |

**Reading the costs.** Generation is a load-time cost: about 0.6 s of geometry and about 10 s with maps for one of
every item here; a 2,048-texel set costs about 2 s to paint and about 0.2 s to upload with CPU mips on llvmpipe.
Animation is cheap per soldier, and most of it is the hold constraint (finger wrap and arm IK), the first
candidate for caching (the wrap depends on the grip radius and the hand's frame, not on the clip). **The draw cost
per kitted soldier is the real problem:** about 71 draw calls and 0.57 million triangles per soldier across the main and shadow passes, because every item is drawn at LOD0 into every pass and
each part is its own draw. Before kit reaches a crowd it needs distance LODs (the hero already has two; the other
generators take a `Lod` parameter that is not yet chosen by distance), cheaper shadow casters, and merged kit meshes
per soldier. Nothing here was measured on a GPU.

## 6. Tests and builds

`Gfx.Tests/KitTests.cs`, 18 tests. `[Fact]`: run everywhere. `[CharsBakeFact]`: skip without the character bake.
`[CharsGfxFact]`: skip without a GL device or the bake.

| Test | Asserts |
|---|---|
| SocketsSitOnTheBodyNotOnTheHands | hip sockets on the pelvis, within 0.20 m of the hip joint, more than 0.25 m from the hand, outside the hip joint; forearm sockets 3–12 cm along and 2–6 cm off the forearm; back behind the chest; head within 12 cm of the head bone; all 10 identities |
| StrapShieldFitBringsTheFistToTheHandGrip | fist within 30 mm of the hand grip for the aspis and for the kite with a 28 mm stand-off, all 10 identities; slide within ±12 cm; unit wrist rotation |
| GripWrapClosesTheFingersOnTheShaftWithoutPassingThrough | no finger joint inside the shaft; wrap error under 15 mm; curl shrinks as the grip thickens |
| CentralGripShieldTurnsAboutItsBarToFaceTheWantedWay | the chosen spin faces the wanted direction at least as well as any spin on a fine sweep |
| GeneratorsAreDeterministicAndSeedsVary | equal hashes for equal specs (mesh and maps); different seeds and different heads give different items |
| GeneratedMeshesAndMapsAreWellFormed | finite positions, unit normals and tangents, valid indices, UVs inside the atlas, map sizes, normal texels unit and facing out, frames present; every kind |
| HeightBumpsBecomeNormalsTiltingAwayFromTheirTop | a painted dome's normals tilt the right way in x and in image-up y |
| TangentFramesMatchTheNormalMapConvention | tangent along +u, bitangent towards image-up (glTF) |
| PngRoundTripsBitExactly | RGBA and RGB images through the encoder and decoder |
| HeroBakeIsByteIdenticalAndRoundTrips | two bakes byte-identical; three LODs decreasing; LOD0 indices, positions and UVs and the maps equal after reading back |
| AuthoredClipsReachTheirHandTargets | grip within 2 cm and 5 degrees of the key's target; feet on their targets; nothing below the ground |
| AuthoredClipsAreDeterministic | two bakes give equal rotations and pelvis tracks; unit quaternions |
| DeathsEndLyingOnTheGroundAndSurrenderKneels | deaths end with pelvis under 0.30 m and head under 0.35 m, no joint below the ground; surrender kneels with hands up |
| HelmetsHideTheHairUnderThemButNotTheFace | under 60 % of the hair kept; the crown covered, the eye not |
| CarriedItemsHangFromTheirSockets | a scabbard's throat within 6 cm of `hip_l`, tip at least 0.5 m below |
| BowLimbsBendSoTheStringReachesTheHand | each string half keeps its length within 2 mm at three draws; a longer draw bends more |
| KitLayerReferencesNoSimulationAssembly | DR42: no `Sim.*` or `Proto*` reference |
| HeroSceneRendersTheAspis | a device still of `kit-hero` shows bronze at the centre and draws something |

During the run the tests found three defects before the final captures: a vertex no triangle used left with a zero
normal in the great helm (now every unused vertex gets a unit normal), a forward death propped up 0.45 m by a hand
pushed into the ground (the lying key now puts the hands beside the thighs, and the ground clamp keeps every joint up,
not only the listed ones), and the scene test asserting a draw count it never started counting.

**Builds and runs on the final tree** (Release and Debug, `M7Gfx2.slnx`, `-m:1`, niced; llvmpipe):

- Debug build: 0 warnings, 0 errors. Release build (`--no-incremental`): 0 warnings, 0 errors.
- Gfx tests, Release: 241 passed, 0 failed, 0 skipped (1 min 24 s). Debug: 241 passed, 0 failed, 0 skipped (2 min
  31 s). Per class, both configurations: Avatar 18, Camera 8, Chars 24, Cinematic 26, EffectLoad 4, EngineUnit 6,
  EnvLayout 12, EnvRender 3, EnvTree 6, Frustum 5, Integration 2, **Kit 18**, PerfDevice 10, PerfEnvTap 1,
  PerfProcess 5, PerfUnit 41, Render1Math 17, Render1Render 7, Render 2, Terrain 15, Units 11. The 223 tests that
  existed before this stage all still pass. The whole suite ran in one host; no class split was needed.
- `scripts/check-gfx-determinism.sh`: OK (4 files of `Gfx.Proc/Authoritative` scanned).
- `scripts/bake-kit.sh --check`: identical. `scripts/bake-kit-outfits.sh --check`: 12 files compared, 0 differ.
- `prototypes/m7-gfx1` is untouched (`git diff 1601c74..HEAD -- prototypes/m7-gfx1` is empty).

## 7. What still looks placeholder, and limitations

1. **Items other than the hero** are prototype quality. Their materials are procedural and read well from line
   distance; close up, the helmets lack the hammered irregularity of the hero's bronze, the iron reads too bright and
   clean where it is matte (the axe and great helm look whitish under a noon sky), the helmet interiors have no
   padding, and the display posts' head forms read as balls. Shield faces are clean painted fields with chips; no
   repainting, mud or blade damage.
2. **Body armour** comes from the garment bake (linen corselet, bronze cuirass, greaves, mail), not from Gfx.Gear:
   no scale armour, no plate harness, no uniforms yet (the brief's "later" items), and the musket and sling exist
   only in the gallery (no loading or firing animation).
3. **Animation:** key-to-key motion with eased segments, no secondary motion (crests, pteruges, cloth and straps do
   not swing; the standards' cloth is a bone-chain wave, not a simulation), no hand-to-hand contact between
   performers, no weight shift in the guards beyond breathing. The side cut passes close behind the shield's edge.
   Deaths are poses reached by IK, not physics. The walk under the guard layer is CMU motion capture
   (PROTOTYPE-ONLY).
4. **Grips are approximate:** the finger wrap closes on a cylinder; flat grips (the kite's enarmes, the bow's
   handle) and the thumb's opposition are not modelled exactly; a fist can show a millimetre-scale gap.
5. **No distance LOD for kitted soldiers.** `CharInstance.Lod` is chosen by the caller and no kit scene sets it, so
   everything is drawn at LOD0. At LOD1 or LOD2 a kitted soldier would be wrong: only the outfit's merged LOD mesh is
   drawn, so the generated helmet (worn kit has no LOD mesh) and the props would need their own LODs, and the
   stage-chars medieval outfits' LOD meshes still contain the chars bake's old helmets and shields.
6. **Cost:** §5. Not ready for a crowd.
7. **Gallery framing:** the helmets overview is small; the musket close-up does not show its lock well.
8. **The hero itself** is at the bar for this project's pipeline, not beyond it: its detail is procedural (no sculpted
   high-poly bake), its device and wear come from one parameter set (variation by seed is tested, art direction of
   many variants is not), and it has been seen only on llvmpipe, never on a GPU or on the Victus.

## 8. Reproducing

```
export DOTNET_CLI_USE_MSBUILD_SERVER=0 SDL_VIDEODRIVER=offscreen LIBGL_ALWAYS_SOFTWARE=1
cd prototypes/m7-gfx2
nice -n 19 dotnet build M7Gfx2.slnx -c Release -m:1 -nodeReuse:false
scripts/bake-chars.sh                    # stage chars (needed by every kit scene but kit-gallery)
scripts/bake-kit-outfits.sh [--check]    # garments of the kitted forces
scripts/bake-kit.sh [--check]            # the hero aspis glTF
dotnet Gfx.Demo/bin/Release/net10.0/Gfx.Demo.dll --capture kit-hero --out DIR [--set samples=16]
dotnet Gfx.Demo/bin/Release/net10.0/Gfx.Demo.dll --kit-costs
python3 scripts/review-sheets-kit.py S16_DIR CAPTURE_DIR reports/captures/kit
```
