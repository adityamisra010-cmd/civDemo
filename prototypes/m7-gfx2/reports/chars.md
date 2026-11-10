# G2 stage chars: soldiers, commander and avatar

Stage agent chars, branch `g2/chars`, 2026-11-10 (continued from the run interrupted at 00:48 the same night; its
uncommitted work was committed as found, commit `317d4d8`). Scope: DR45 and DR49 item 3, the soldier, commander and
avatar demonstrator, built on the MakeHuman CC0 core and the MPFB2 53-bone game-engine rig. Every number below was
measured in this container on **llvmpipe (Mesa 25.2.8, LLVM 20.1.2, 4 CPUs): llvmpipe, not Victus evidence**. No
hardware GPU ran this code; the CPU figures are this container's 4 virtual CPUs, not the Victus's.

**Verdict, honestly.** G1's characters were boxes with painted faces. G2 now has ten distinct people: the player
avatar (G1's PlayerFixture identity), the force A commander and eight soldiers, four per force, each built from the
MakeHuman CC0 body with its own proportions, face, skin tone, eyes, hair and beard. They wear layered period kit
(braies, hose, shoes or boots, tunics, quilted gambesons, mail, surcoats with woven borders, belts, kettle hats and
nasal helmets, a bronze cuirass and crested helmet for the ancient era, painted shields, spears and swords), carry
five facial expressions, have LODs of about 3,000 and 1,500 triangles, and move with CMU motion capture through a
cosmetic animation layer. The before/after sheet
(`chars-01-g1-vs-g2.jpg`) shows the size of the step. In my judgement the close shots now clear the D-078 floor for
characters, but this is **not production quality**: the garments are shells over the body with procedural folds
only on skirts and at the belt, the skin comes from 512-texel MakeHuman textures, hair is shells and cards without
flyaways, the combat motion is generic mocap rather than authored spear-and-shield fighting, and the CMU clips are
PROTOTYPE-ONLY. Section 7 lists what still looks placeholder. Looking at this run's own first complete captures
found five more defects (torn edges on the bronze cuirass and helmet, boot shafts showing through pale hose, two
pinholes in a collar, a mail skirt through a surcoat, hose through a skirt); they are fixed, with tests where a test
can see them, and `chars-10-fixes-in-run.jpg` shows each before and after.

## 1. What now exists

| Part | Where | What it does |
|---|---|---|
| Bake tool | `tools/charbake/` (Python, numpy, Pillow), `scripts/bake-chars.sh` | Builds 10 identities, 12 outfits (the avatar in three eras, the commander, eight soldier kits), their LODs, 10 clip files and the shared textures into `$G2_ASSETS/baked/chars` (73 files). Deterministic: seeded numpy, single-threaded, canonical JSON; `--check` re-bakes in memory and compares every output byte for byte (§6). Inputs are re-hashed against `assets/manifest.tsv` before every bake |
| Identity (`<id>.g2ch`) | `identity.py`, `mhcore.py`, `rig.py`, `expressions.py`, `skin.py`, `hair.py`, `teeth.py` | hm08 base mesh morphed by macro and regional modifiers (stature solved to the spec), MPFB2 game-engine skeleton fitted to the body plus two eye bones, 4 joint influences per vertex (bytes summing to 255), CC0 eyes fitted to the face, procedural teeth and tongue, five expression blend shapes from the CC0 face pose units (blink, battle cry, pain, grim, calm), skin albedo, mask and painted hair, fur-shell beards and scalp hair with flow-aligned UVs, card brows, lashes and tied-back tails, sockets for grips and the shield strap |
| Outfit (`<id>__<outfit>.g2ch`) | `outfits.py`, `garments.py`, `recipes.py`, `kit.py`, `materials.py`, `lod.py` | Garment shells cut by smooth fields with woven borders split along a distance iso-line and turned-back hems (cut-out islands under 150 skin vertices kept, so a field never punches pinholes); fitted parametric skirts with hanging folds; fitted shoes, and boot shafts made from the hose's own surface; belt-gathered torso folds; skull-fitted helmet bowls (kettle hat, nasal helmet, felt cap, tricorne); the bronze cuirass and the crested helmet as shells whose edges are cut on a smoothed copy of the shell itself, with islands dropped and edges faired; spear, sword and three shield shapes with painted original heraldry; the outfit's view of the identity (visible skin, hair under the helmet, the beard under cheek plates); LOD1 and LOD2 |
| Clips (`<id>__clips.g2ch`) | `retarget.py`, `clips.py`, `bvh.py` | 18 CMU clips retargeted to each identity's rig (walk, jog, run, march, two idles, thrust, two swings, block, cower, kneel, two deaths, stumble, wounded walk, bow). Kept out of the identity files because they are PROTOTYPE-ONLY |
| Runtime | `Gfx.Chars/` | Reader (`Format/CharacterFile.cs`), GPU skinning from a bone-palette texture with a CPU-blended morph stream (stream 1), material shaders (§2), LOD selection, sockets (`CharInstance.SocketWorld`), headgear on and off, the scene stage (`CharStage`) |
| Animation | `Gfx.Chars/Animation/` | Stride-synchronised locomotion blend (idle, walk, jog, run), 0.2 s crossfades, a masked upper-body clip layer, arm stances by two-bone IK with hand orientation, additive procedural hit reactions, a head and eye look with a clamped total turn, hand grips, two-bone foot IK with heel-down foot pinning, clip phase offsets per character. A fixed-step driver (`AnimDriver`) makes every still reproducible |
| Scenes | `Gfx.Demo/Scenes/CharsScenes.cs` | `chars-hero`, `chars-eras`, `chars-forces`, `chars-lod`, `chars-anim`, on the render1 photographic meadow of the temperate fixture map with G1's placeholder props switched off |
| Costs | `Gfx.Demo --chars-costs` (`Gfx.Demo/CharsCosts.cs`) | Animation CPU per character per frame, skinning prep, expression blending, planted-foot slide (§5) |
| Tests | `Gfx.Tests/CharsTests.cs` | 24 tests (§6) |
| Review sheets | `scripts/capture-chars.sh`, `scripts/review-sheets-chars.py` | §4 |

Identities, outfits and triangle counts of the final bake (from the files' INFO and MESH chunks; identity triangles
include eyes, teeth, tongue, hair shells and cards; LOD0 is every part):

| Identity | Kind | Stature | Identity tris | Outfit | Outfit tris (LOD0 garments+kit) | LOD1 | LOD2 |
|---|---|---:|---:|---|---:|---:|---:|
| avatar | avatar | 1.800 m | 69,838 | era_ancient (Avatar, ancient (military)) | 30,115 | 3,165 | 1,596 |
| avatar | avatar | 1.800 m | 69,838 | era_early_modern (Avatar, early modern (military)) | 29,743 | 3,194 | 1,640 |
| avatar | avatar | 1.800 m | 69,838 | era_medieval (Avatar, medieval (military)) | 43,447 | 3,180 | 1,638 |
| commander_a | commander | 1.840 m | 69,746 | kit_a (Force A commander: mail, surcoat, gilt-rim shield) | 44,265 | 3,240 | 1,563 |
| a1 | soldier | 1.740 m | 46,621 | kit_a (Force A: gambeson_spear) | 23,802 | 3,230 | 1,657 |
| a2 | soldier | 1.701 m | 47,418 | kit_a (Force A: mail_spear) | 32,228 | 3,158 | 1,608 |
| a3 | soldier | 1.820 m | 66,326 | kit_a (Force A: surcoat_sword) | 42,461 | 3,175 | 1,593 |
| a4 | soldier | 1.686 m | 90,842 | kit_a (Force A: levy_spear) | 32,154 | 3,156 | 1,627 |
| b1 | soldier | 1.720 m | 41,882 | kit_b (Force B: gambeson_spear) | 23,798 | 3,194 | 1,671 |
| b2 | soldier | 1.780 m | 66,782 | kit_b (Force B: mail_spear) | 32,260 | 3,184 | 1,600 |
| b3 | soldier | 1.731 m | 47,318 | kit_b (Force B: surcoat_sword) | 42,529 | 3,202 | 1,673 |
| b4 | soldier | 1.760 m | 70,979 | kit_b (Force B: levy_spear) | 32,252 | 3,142 | 1,626 |

## 2. Rendering (Gfx.Chars)

All character effects are GLSL 1.20 through the engine's MGFX builder and use the render1 lighting library (sun GGX
with cascaded shadows, sky or HDRI image-based lighting, aerial perspective), so characters stand in the same light as
the terrain. Skinning reads a per-frame bone palette (one RGBA32F texture row per character, three texels per bone,
vertex texture fetch) and adds the expression morph stream (stream 1, blended on the CPU only when the weights change)
before skinning.

| Material | Shading |
|---|---|
| Skin | Dual-lobe GGX (F0 0.028), tiling micro-normal pores, per-channel wrapped diffuse on progressively smoother normals (red scatters furthest) and red-shifted shadow edges as the subsurface approximation, transmission through ears and nostrils, cavity occlusion from the albedo alpha, a dark warm mouth interior |
| Eyes | Iris parallax under the cornea, limbal darkening, socket occlusion; the clear cornea shell is drawn afterwards with additive GGX and a sharp environment reflection (the wet highlight) |
| Hair and fur | Fur shells for scalp hair and beards (strand texture, hashed alpha resolved by the jittered still samples, strand-aligned Kajiya-Kay shading, inner shells darker); cards for brows, lashes and tied-back or long hair; painted strands in the skin under all of them |
| Cloth | Linen, wool, quilted, leather, felt: triplanar weave in bind space turned into a derivative bump, dye unevenness, woven borders (four original tablet-weave patterns through UVs on the border band), soles, sun-faded tops, mud spatter below the knee, sheen |
| Metal | Iron, steel, bronze, gilt and riveted mail: hammer bump, broad low-contrast tarnish, oxide specks only where the wear is high, mail rings fading to their average below a pixel; reflected sky light keeps 40 % of its chroma and the horizon band is lifted, so iron reads grey, not blue |
| Paint and wood | Painted shield faces from the emblem texture with chips gathering at the rim and plank seams; wood grain |
| LOD | One single-material shader: vertex albedo with a roughness/metal code, the skin texture for skin and the emblem texture for shield faces |

Skirts (mesh flag 1) are pushed out of four leg capsules (thigh and calf of each leg, from the posed skeleton) in the
vertex stage, so a striding or running leg cannot show through the cloth. The capsules reach 4 mm past the outermost
point of the skin and of the outfit's legwear (hose, braies, boot shafts, greaves), one 6 mm step wider for each skirt
layer from the innermost out, so a layered skirt never lands on the one over it.

## 3. Lessons fixed: the lost attempt's, and this run's own

| Lesson (from the brief) | Fix | Evidence |
|---|---|---|
| Garment shells had jagged, torn-looking hems and necklines | Every edge is the straight-segment contour of a smooth field; the border colour is no longer a vertex-colour band (which blotched along triangles) but a separate band split off along the iso-line "distance to the edge = border width", textured with a woven pattern through its own UVs; every edge has a turned-back hem | `chars-03-faces.jpg` (necklines), `chars-06-forces.jpg` (armholes, hems) |
| Skin showed through at the shoulders in some poses | Garment shells carry the skin weights of the vertices they were made from, skin under a garment is removed from the outfit's visible-skin view with a 12 mm margin, and the LODs drop inner layers before simplifying | `chars-09-anim-actions.jpg` (raised arms, kneeling), `chars-07-lod.jpg` |
| The mouth interior was tinted purple | The inside-mouth mask drives a dark warm interior with strong occlusion; teeth, gums and tongue have their own shader with occlusion growing towards the throat | `chars-04-expressions.jpg` (battle cry, pain) |
| Skin looked slightly plastic in strong sun | Dual-lobe skin specular with roughness 0.5/0.28 and a 15 % second lobe, pore micro-normals, fine albedo speckle and spots added to the 512-texel source, the wrapped subsurface terminator | `chars-03-faces.jpg` (10:30 sun and 17:36) |
| Iron helmets read blue from sky reflections | Steel F0 0.56, iron 0.46; reflected sky light keeps only 40 % of its chroma, the horizon band is lifted towards the haze colour, the ground below the horizon is reflected; tarnish is broad and low-contrast | `chars-02-hero-turntable.jpg`, `chars-06-forces.jpg` |
| Cloaks were skinned panels that legs pushed through | No cloaks. Skirts are fitted lofts weighted to the pelvis and thighs and pushed out of the leg capsules on the GPU | `chars-08-anim-locomotion.jpg` (walk and run frames) |
| The PROTOTYPE-ONLY MakeHuman brow, lash and ponytail meshes were used | None used: scalp hair and beards are fur shells, brows and the tied-back tail are cards grown in code, lashes sit on the CC0 lash helpers, all strands painted in code | PROVENANCE §3.9 |
| (Found in this run's own captures, round 1) The bronze cuirass's neck and armhole cuts merged into one, leaving spiky shoulder ends and skin visible between bronze and tunic; the crested helmet's cheek plates hung from a rim that crossed the ears and came out torn | Cuirass: separate armholes (the arm weight smoothed over the skin in 40 passes, cut at 0.5) keep 10 cm shoulder straps; the neck and hip edges are cut on a smoothed copy of the shell itself; stray islands dropped, edge loops faired, 6 mm rolled hem; the tunic runs whole underneath, at least 8 mm under the bronze. Helmet: one shell of the skull and face whose lower edge is a smooth function of the azimuth (brow line, cheek plates, an arch over the ear, nape); the beard under the cheek plates is trimmed by the outfit's view and returns when bare-headed | `chars-10-fixes-in-run.jpg`, `chars-05-eras.jpg`; test `CheekPlatesTrimTheBeardOnlyWhileWorn` |
| (Found in this run's own captures, round 1) Black boot shafts showed through pale hose in patches; the early modern shirt had two pinholes at the collar | The MakeHuman tights helper the hose is made from stands 6 to 22 mm off the skin, so a shaft offset 8 mm from the skin lay under the hose in places: shafts worn over hose are now the hose's own surface 4 mm further out. The shirt's neck field dipped into the hollows above the collarbones and cut two small islands: cut-out islands under 150 skin vertices are now kept | `chars-10-fixes-in-run.jpg`, `chars-05-eras.jpg` (early modern legs and collar) |
| (Found in this run's own captures, round 1) The commander's mail skirt showed through his surcoat in a stride | Two causes, both fixed: every skirt was pushed out to one shared leg capsule, and skirt weights followed each skirt's own length, so the short mail skirt followed the thigh more than the long surcoat over it. Skirts are now pushed out in layers (6 mm more per layer, innermost first) and weighted by absolute height from the hips to the knee, the same for every skirt | `chars-10-fixes-in-run.jpg`, `chars-06-forces.jpg` (force A); test `LayeredSkirtsArePushedOutInLayerOrder` |
| (Found in this run's own captures, round 2) The hose showed through the levy b4's tunic skirt in two dark tongues at the thighs | Skirts were fitted to the skin's section and pushed out of capsules sized from the skin, but hose and braies are made from the MakeHuman tights helper, which stands up to 22 mm off the skin. Skirts are now fitted to the section of the skin and of the legwear already made (minimum rest clearance over the hose or braies 1.3 to 7.6 mm, measured for b4, a1, the commander and the ancient avatar; b4's was -5.7 mm before), and the push-out capsules reach the outermost point (over the middle 70 % of each bone) of the skin and of every non-skirt leg layer of the outfit, plus 4 mm: for b4's thigh 128 mm, where the skin's 90th percentile plus 10 mm gave 114 mm and the hose reaches 124 mm | `chars-10-fixes-in-run.jpg`, `chars-06-forces.jpg` (force B); test `SkirtCapsulesClearTheLegwear` |
| The retargeter's slide-before-lock numbers were implausible (156–184 cm/s) and never explained | Measured both ways (§5): with a static root (in-place playback, the instance not moving) a planted foot slides 130 cm/s at 1.45 m/s and 336 cm/s at 3.6 m/s, because the in-place clip moves the planted foot backwards at about the clip's speed. That is the likely origin of the lost attempt's figures (its root movement is not known). With the root moving at the animation speed the residual is 20 cm/s at a walk before locking and 0.05 cm/s after. The metric now follows one contact point (ball or ankle) frame to frame | `Gfx.Demo --chars-costs`; test `PlantedFeetStayStillOnAMovingRoot` |

## 4. Captures

Full-size PNGs (1280×720, 16 jittered samples, never committed):
`/tmp/claude-0/-home-user-civDemo/54b82655-610f-500e-a185-8fae64bdd108/scratchpad/m7gfx2/captures/chars/final12/`
(60 frames, the five scenes of `scripts/capture-chars.sh`, from the final bake and build, 13 min 26 s on llvmpipe).
The folders beside it are earlier rounds. Round 1, `final`: the first complete capture of this run, from bake 6
(commit `0ab2993`), the fall frames re-taken at later clip times from bake 7 and the eras frames from bake 9 (commit
`105f876`; bakes 7 to 9 changed only the ancient outfit). Round 2, `final10`: bake 10 (commit `0821799`). These hold the frames in which this run found the
defects listed in §3 (sheet `chars-10-fixes-in-run.jpg`). `final11` (bake 11 with the earlier capsule sizes) was
superseded by a runtime change; `w1`–`w8` are working rounds. G1 BEFORE frames are the pristine G1 captures stage render1 rendered from G1's own build
(`…/captures/render1/g1/`). Every review sheet's legend names the renderer (llvmpipe) and the one PROTOTYPE-ONLY
input in frame, the CMU motion capture that poses the characters. I looked at every final frame: the 56 the sheets
use, and the four they leave out (`death-0`, `death-2`, `surrender-0`, `look-1`) at full size.

Review JPEGs (committed, `reports/captures/chars/`):

| Sheet | Shows |
|---|---|
| `chars-01-g1-vs-g2.jpg` | BEFORE/AFTER pairs: G1's avatar turntable and face against G2 with the same cameras; G1's avatar eras against G2's three eras with the same camera; G1's soldiers against G2's two forces |
| `chars-02-hero-turntable.jpg` | The player avatar in the medieval kit, eight 45-degree steps |
| `chars-03-faces.jpg` | Face close-ups bare-headed (front, three-quarter, profile), the kettle hat, the 17:36 sun, the commander |
| `chars-04-expressions.jpg` | Neutral, battle cry, pain, grim, calm, blink |
| `chars-05-eras.jpg` | One identity in three eras: line-up, three-quarter, the three faces |
| `chars-06-forces.jpg` | Force A (four soldiers and the commander) against force B (four soldiers): both lines, low, each force, 17:36, the commander |
| `chars-07-lod.jpg` | LOD0, LOD1, LOD2 side by side; faces; at 6 m, 18 m and 40 m |
| `chars-08-anim-locomotion.jpg` | Six frames of one walk cycle and four of the run, on a moving root with feet locked |
| `chars-09-anim-actions.jpg` | Thrust, block, additive hit, fall, surrender (kneel, hands raised), head look |
| `chars-10-fixes-in-run.jpg` | The defects this run found in its own first two capture rounds (cuirass and helmet edges, boot shafts through hose, shirt pinholes, mail through the commander's surcoat, hose through b4's skirt) next to the same frames from the final bake (`scripts/review-sheet-chars-fixes.py`; the round 1 and 2 frames come from the commits named above, not from the final code) |

## 5. Costs (llvmpipe and this container's CPU: not Victus evidence)

`Gfx.Demo --chars-costs`, run three times on this container (4 virtual CPUs; Mesa llvmpipe for the one GL step) with
nothing else running; the ranges are over the three runs. The CPU figures are this container's CPUs, not the
Victus's; the morph-stream upload goes to llvmpipe (system memory), not to a GPU. The output labels every line
"SOFTWARE RENDERING (llvmpipe) - not Victus performance".

| Cost | Measured (three runs) | What it covers |
|---|---|---|
| Animation, every layer | mean 23.4 to 24.6 µs, p50 8.5 to 11.8 µs, p95 55 to 61 µs per character per frame | One `CharAnimator.Step` at 60 Hz: locomotion blend with stride sync, 0.2 s crossfades, masked upper-body clip, stance IK, additive hit, head look, two-bone foot IK with locking (11,400 steps over the 10 identities per run) |
| Animation, locomotion only | mean 4.4 to 5.0 µs, p95 10.4 to 11.1 µs | The same without IK, layers or look |
| Skinning preparation | mean 9.9 to 11.3 µs, p95 10.2 to 11.2 µs per instance | Forward kinematics and 55 skin matrices (the bone-palette upload is separate) |
| Expression blend | mean 110 to 127 µs, p95 188 to 226 µs per instance per change | The five expression shapes blended on the CPU into the morph stream and uploaded; 14,759 morphed vertices on average; runs only when the weights change |

Planted-foot slide (mean over the 10 identities; the three runs printed identical slide lines): walking at 1.45 m/s
on a moving root, 20.1 cm/s before locking (worst identity 21.7) and 0.05 cm/s after (worst 0.28); running at 3.6 m/s, 40.6 cm/s before (worst 56.9) and 0.001 after. With a static root
(in-place playback, the instance not moving) the planted foot slides 129.6 cm/s at a walk (worst 132.8) and
335.8 cm/s at a run (worst 349.5) before locking, 1.2 cm/s and 0.001 cm/s after: that is the lost attempt's kind of
figure (§3).

A frame of the two forces (`Gfx.Demo --bench chars-forces --shot lines-34 --frames 20 --profile`, 1280×720, nine
characters at full detail on the meadow, cascaded shadows, one sample per frame): p50 1,071.7 ms, p95 1,160.7 ms per
frame, 895 draws, 2.80 million triangles; the scene pass 757.9 ms and the shadow pass 276.4 ms of it. llvmpipe, not
Victus evidence: it only shows where the work is (characters at LOD0 dominate both passes), not how fast a GPU is.

Bake (`scripts/bake-chars.sh`, inputs re-hashed first, then single-threaded numpy): 15 min 15 s wall for all 73 files
of the final bake. `--check` of the final bake took 23 min 29 s wall while the captures ran beside it; an unloaded
`--check` of bake 9 took 13 min 43 s.

## 6. Tests, builds and determinism

| Check | Result |
|---|---|
| Builds, Release and Debug, full rebuild (`--no-incremental`, `-m:1`, `-nodeReuse:false`, solution `M7Gfx2.slnx`) | 0 warnings, 0 errors each |
| Gfx tests, Release (`dotnet test Gfx.Tests -c Release`, llvmpipe, unloaded) | 201 passed, 0 failed, 0 skipped (56 s) |
| Gfx tests, Debug | 201 passed, 0 failed, 0 skipped (1 min 49 s) |
| Per class (both configurations) | AvatarTests 18, CameraTests 8, CharsTests 24, CinematicTests 26, EffectLoadTests 4, EngineUnitTests 6, FrustumTests 5, IntegrationTests 2, PerfDeviceTests 10, PerfProcessTests 5, PerfUnitTests 41, Render1MathTests 17, Render1RenderTests 7, RenderTests 2, TerrainTests 15, UnitsTests 11: all passed |
| `scripts/bake-chars.sh --check` on the final bake | `charbake --check: 73 files compared, 0 differ` |
| G1 (`prototypes/m7-gfx1`) | untouched: `git diff 1601c74..HEAD -- prototypes/m7-gfx1` is empty |

The suite ran whole in each configuration; no class group had to run on its own. The 24 stage-chars tests:
`G2chPopulatedRoundTripIsBitExact` (a populated file of every chunk kind round-trips bit for bit),
`G2chRejectsTruncatedAndCorruptData`, `BakeManifestMatchesTheFiles`, `EveryBakedFileParsesCompletely`,
`ClipsLiveInTheirOwnPrototypeOnlyFiles`, `AvatarStartsFromTheG1PlayerFixture`, `EraOutfitsShareTheAvatarsFaceAndBody`,
`TwoForcesOfDistinctIdentities`, `LodsMeetTheirTriangleBudgets` (2,400 to 3,600 and 1,100 to 1,800),
`FurShellTrianglesStayWithinTheirLayer`, `ExpressionsAreBoundedAndBlinkClosesTheLids`,
`CrossfadeTakesTwoTenthsOfASecond`, `PlantedFeetStayStillOnAMovingRoot`, `HitReactionIsAdditiveAndDecaysToTheBasePose`,
`UpperBodyMaskLeavesTheLegsAlone`, `HeadLookIsClampedToNeckLimits`, `TwoBoneIkReachesAReachableTarget`,
`SocketWorldCarriesTheSwordGrip`, `ErasShareOneIdentityAssetAtRuntime`, `CheekPlatesTrimTheBeardOnlyWhileWorn`,
`LayeredSkirtsArePushedOutInLayerOrder`, `SkirtCapsulesClearTheLegwear`, `HeroSceneRendersADressedCharacter`,
`CharacterLayerReferencesNoSimulationAssembly`. Of the three added in this round, `CheekPlatesTrimTheBeardOnlyWhileWorn`
was run against the bake-9 data and failed there (no beard view), and `SkirtCapsulesClearTheLegwear` fails by
construction against the old skin-only capsules (its second assertion measures the hose beyond them).

## 7. What still looks placeholder or wrong

Said plainly, from looking at every capture:

- **Garments are shells over the body.** Skirts have hanging folds and torsos fold at the belt, but sleeves, the
  backs of surcoats and the hose are smooth and read as painted on in close shots. There is no cloth simulation:
  skirts follow the thighs by skinning and are only pushed out of the legs.
- **Skin** comes from MakeHuman's 512-texel CC0 skins, upscaled and tone-matched, with procedural speckle and a tiling
  pore normal. Close up the faces are clean and slightly soft; there are no wrinkle maps for the expressions.
- **Hair** is fur shells, painted strands and cards: tied-back and cropped hair and beards read well at mid range;
  long hair (two levies) is sparse cards, and there are no flyaways or stray hairs on any style.
- **Hands** are MakeHuman hands with procedural finger curls: grips close round a weapon but do not wrap it exactly,
  and the left hand holds no shield strap.
- **Animation** is generic CMU motion capture: one early-session swordplay clip, lunges and blocks from a non-weapon
  series, one fall and one kneel. There is no authored spear-and-shield fighting, no dodges, no formation drill; a
  surrendering soldier's weapons are hidden, not dropped on the ground. The run is the CMU jog blended towards a
  sprint, and the ancient spear at rest is held one-handed by IK.
- **LODs** are single-material simplifications: at LOD2 faces lose definition and painted borders become flat
  colour bands.
- **Equipment** is generated in code (lathe and extrusion shapes): helmets and shields have clean silhouettes but no
  rivets, straps or lacing; the kettle hat's brim has no rolled edge.
- **The bronze cuirass** is a smoothed shell of the torso: its musculature is only what survives the smoothing, it
  bends with the torso as the skin does instead of moving as a rigid plate, and its hip edge hides under the leather
  strips. The crested helmet's cheek plates are rigid on the head bone, so a battle-cry jaw drop would move the
  cheeks under them (the helmeted figure keeps a calm face in these scenes).
- **Collars**: a scan of every garment edge of the final code (boundary loops under 12 cm that are not the coincident
  seam of a woven border) finds one remaining kind of flaw, a small
  keyhole notch at the throat of three garments (the levy a4's tunic and jacket, b1's gambeson) where the neck cut
  pinches over the throat. It is not intended; it does not show in these captures (shields cover those throats in
  the force shots, and the running b1 is seen in profile).
- **The setting** is render1's empty meadow (G1's placeholder trees and rocks are switched off in these scenes); the
  battlefield environment is another stage's work.

## 8. Limitations and notes

- The CMU clips are **PROTOTYPE-ONLY** (provisional terms) and sit in their own files; replacing them with licensed or
  authored animation changes no identity or outfit file.
- Expression blending is on the CPU (110 to 127 µs mean per change and instance here, §5); it runs only when weights
  change.
  A GPU morph path (texture of deltas) would be needed for crowds with live faces.
- The bone palette is per character per frame (55 bones): fine for tens of hero characters; crowds belong to the
  instanced GPU crowd path of a later stage (LOD meshes are its input).
- The skirt push-out uses four leg capsules and runs only on skirt parts; arms can still clip shields in extreme
  poses (no arm capsules).
- Foot locking pins the ankle horizontally while the heel is down and lets go at toe-off; the foot rolls over the
  ball without its own lock, so a little toe slide remains in fast runs.
- Statures: the solver takes one secant step from bodies measured at height macro 0.5 and 0.6, and the macro changes
  slope at 0.5, so identities that need less land off target: a4 is 1.686 m for 1.67 m asked (16 mm), a2 and b3
  within 1.4 mm, the other seven within 0.1 mm (the avatar 1.800 m, the fixture's 1,800 mm).
- Rendering never touches simulation state: the character assembly references no simulation project (test
  `CharacterLayerReferencesNoSimulationAssembly`), drivers take scripted or published inputs only, and the avatar
  is purely visual (DR39, DR42).

## 9. Reproduce

```
export DOTNET_CLI_USE_MSBUILD_SERVER=0 SDL_VIDEODRIVER=offscreen LIBGL_ALWAYS_SOFTWARE=1
cd prototypes/m7-gfx2
scripts/fetch-assets.sh                       # pinned sources (idempotent)
scripts/bake-render1.sh                       # the meadow's ground materials
scripts/bake-chars.sh                         # characters; --check re-bakes and compares byte for byte
nice -n 19 dotnet build M7Gfx2.slnx -c Release -m:1 -nodeReuse:false
nice -n 19 dotnet test Gfx.Tests/Gfx.Tests.csproj -c Release --no-build
scripts/capture-chars.sh OUT                  # every chars scene, 16-sample stills, then --chars-costs
python3 scripts/review-sheets-chars.py G1_CAPTURES OUT reports/captures/chars
```
