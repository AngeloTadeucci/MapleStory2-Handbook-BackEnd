# Non-effect replacement status, 2026-09-06

## Motion, coverage and shaders implemented, 2026-09-06

The user authorized all four follow-ups. The default local release is now
`simulator-release-04`, with 144 models and 124 item/body entries: 82 reviewed,
32 preview and 10 unavailable. Gelo is at `/outfits?preview=gelo-05` with twelve
saved instances and original dyes. Sign 11820024 exports its separate four-second Idle_A KF and
the viewer binds its tracks to owned joint UUIDs, independently of body motion.
Both bodies include source star_attack_idle_a and star_run_a. Explicit star
draw/stow preserves hand identity and dyes. Paired back placement still overlaps
at the XML anchor and is labeled unresolved rather than shifted artificially.

Thin Adventurer Cape 11800001 now retains private joints on both bodies. New
complete outfit matrices cover 11400001/2, 11500002/4, 11600003/4, 11700002/4
and robes 12200002/3/4. The all-items
filter omitted database slot-0 full outfits; it now includes exact catalog IDs.
Paired knuckles now use their explicit source attachnode for drawn placement;
the old exports used the back target without the dummy rotation. Female Gelo
and a male hoodie/cape outfit were directly checked with corrected 15500002.
This corrects the earlier claim that those old knuckle exports established
proper placement. Other knuckle entries remain previews.

Material exports retain specular enable flags, colors and powers. The renderer
uses client half-Lambert diffuse and gloss-modulated specular under studio lights.
A first specular render exposed missing enable flags and was rejected; it is
not acceptance evidence. Full client scene/rim/hair shader parity remains open.
Final checks: 35 C# tests, 12 Python tests, 192 focused frontend tests and zero
Svelte errors/warnings. All 144 glTFs validate with zero errors/warnings.
`Diagnostics/refine_simulator.py` and `motion-library-plan.json` re-export the
whole selected library, including source material flags/powers and explicit
knuckle attachnodes. `motion-library-review.json` binds 82 reviewed entries to
exact model, catalog and customization hashes. A second independent build
reproduced all 620 inventory files byte for byte. The 617 release-03 files are
unchanged. Generated libraries and Gelo's appearance snapshot remain ignored.

Production packaging also passes `pnpm build`. The adapter packages only the
selected simulator release under gltf, preserving ordinary application assets
and leaving local candidate libraries and character snapshots in the source tree.
The first build exhausted file handles compressing all local research libraries.
An adapter regression test now verifies the selected-release copy and exclusions.
All 620 files in the final build/client release match the inventory SHA-256 hashes.
The shared release setting lives in src/lib/outfits/simulator-release.json;
placing it at the repository root initially caused Vite's browser allow-list to
reject it with 403. Moving it into src fixed the directly reproduced client 500.
The 11 catalog/API/packaging tests passed again after that correction, bringing
the focused frontend total to 193 including the packaging test. The production
build retains nonfatal dependency, optional-hook-export and chunk-size warnings.
No production server was started or deployed for this packaging check.

After the packaging correction, T3 loaded Gelo's twelve saved instances and dyes
again. At 390x844, the canvas is 343x549 and document width remains 390. The
front fitting_idle_a at 0.3 seconds shows the complete outfit without cropping.
The additional Henesys/Happy PNG export check timed out when T3 reported the tab
invisible; that final combination is not claimed verified. Earlier studio PNG
encoding and the outfit/animation matrices remain separate evidence.

Direct T3 evidence is under `obj/motion/evidence/`. Set A is 11400001,11500002,
11600003,11700002. Set B is 11400002,11500004,11600004,11700004. Both use the
body's reviewed hair/face, 11200001 earrings and drawn stars, with 11800001 cape
added for later checks. Robes replace CL+PA together; either separate piece
removes the complete robe. Female 10200224 + 11300001 switches to C hair and
restores A on removal. Camera framing now measures current deformed vertices,
fixing cached SkinnedMesh bounds that cropped hair after changing poses.

Sign removal/equip repeated five times returns to seven private joints, 26
geometries and 85 textures with the gloss maps enabled. Removal leaves zero
private joints. An intentionally missing second sign part preserves the twelve
items, previous animation and the same resource counts. A numerical regression
compares sign vertex deformation against its independently loaded source clip
while the body runs, at 0,0.5,1,2,3,4 seconds. Draw/stow buttons preserve saved
left/right dyes and expose the paired-back overlap. A single star removes the
whole knuckle bundle. Actual star-pose selection and playback advance the body
from 32.8748 to 35.9026 seconds while the heart position changes independently.
T3 recording: `C:/Users/atade/.t3/userdata/browser-artifacts/browser-recording-mtqj5kyn.webm`.

Reference interaction: hunya female idle_a, 11400002 plus 11500004 coexist;
12200002 clears PA; re-equipping 11500004 clears the full outfit. Our XML
confirms the same CL/PA bundles and exact models. The reference retains prior
dyes RGB63,59,51 and20,18,15 when choosing the hoodie, which does not establish
the NIF defaults. Its weapon UI still explicitly reports unsupported weapons.
No reference assets were copied. Shader scene lighting, rim light, anisotropic
hair behavior and paired star back-placement parity remain unresolved. Cape
cloth physics, effects and particles are excluded. Knuckle stowed rotations
remain unsupported; only the explicit drawn attachnodes are used.

The user authorized committing this checkpoint on 2026-09-06. No effects,
publishing, production writes or process stops occurred. Generated releases,
visual captures and the private Gelo snapshot remain outside Git.

## Gelo missing equipment implemented, 2026-09-06

The active library is now `simulator-release-03`: 142 models and 124 item/body
entries, with 58 reviewed, 54 preview and 12 unavailable. The previous release-02
remains unchanged. Gelo's updated local preview is
`http://127.0.0.1:4000/outfits?preview=gelo-02`. It equips twelve instances,
including back sign 11820024, blush 10400108 and two 13400306 Fire Prism Stars.
All saved colors are retained. These changes are not committed or deployed.

The sign retains its own source joints and sibling MT mesh under the XML
`MT_Point01` to `Scene Root` attachment. Rigid wing meshes keep their declared
source parents. The frontend owns and releases these private joints per item.
Five remove/equip cycles stayed at seven private joints and 26 geometries /
74 textures. A deliberately missing second model retained the previous outfit
and leaked no joints. Source geometry and bind matrices have regression coverage.

Stars use distinct item/hand keys, so dyes and removal are independent. OH means
either hand. RH and LH exports use the client weapon-hand helpers, confirmed in
the installed x64 client at 0x141658150. The XML stowed target is
`Weapon_Back_B_Point`, with female translation (-6,6,0), male (-4,6,0), and zero
rotation. These stowed exports are retained for research, not exposed as a
reviewed stowed-pose workflow. Nonzero dummy rotations fail explicitly.
Two-handed equipment clears both stars, and either star replaces a two-handed
bundle. The left removal button preserves the right star and its dye.

Blush uses our `Item_Makeup/10400107_RosyCheeks.dds`, the saved/client XML
position (0.25,0.01), scale 0.52 and zero rotation. The implementation follows
`MS2CharacterSkinMaterial/Shader0001-P.hlsl` TexCoordTransform2D and alpha blend
on FA_Skin. It survives face changes, expressions and skin recoloring without
substituting geometry. Removal changes the rendered image; reapplying it
reproduces the original image in the tested paused state.

T3 evidence covers complete Gelo front/side/back views at fitting_idle_a 0.3,
fitting_idle_a/run_a/Dance T at 0.75, and Blink/Happy/Angry with blush. Male
10200230, 10300001, 11400350, 11500003, 11600001, 11700001, 11200001 and both
stars were checked in the same views/poses. Male run playback advanced from
0.75 to 2.7722 seconds. Dance T brings stars through the face, and the UI states
that limitation. Baseline poses are not weapon combat poses. The sign's own
wing animation is not included; the local preview and catalog explain this.
Shader differences remain accepted for now, with parity unresolved. Effects
and particles remain excluded.

Reference site: 11820024 selects `11850281_C_MTValentine04.gltf`, with source
red colors (170,47,47) and (96,19,19). Its weapon UI explicitly reports no
weapon support. Makeup supports only 10400056, not Gelo's 10400108. Reference
canvas readback remains transparent, so silhouette parity is unresolved.
Our client supplies all shipped assets and placement data.

`Diagnostics/extend_simulator.py` rebuilds all 18 extension models and 23 source
textures using `gelo-library-plan.json` and `gelo-library-items.json`, then
applies `gelo-library-review.json`. The review checks both hand variants and
the blush texture hash before promotion. All 142 candidate GLTFs validate
without errors or warnings; their bytes and customization match the T3 preview.
Evidence captures are local under `NifToGltf/obj/gelo/gelo02*.jpg`.

Final checks: 33 C# tests, 12 Python tests and 182 focused frontend tests pass,
with zero Svelte errors/warnings. The final extension rebuild reproduces all
617 release-03 inventory entries byte for byte; release-02's 552 hashes still
match. Female playback with all twelve instances advanced from 132.6268 to
136.7156 seconds. The visible catalog left-hand equip button preserves the
right-hand dye. Legacy single-model OH dagger 13100068 now occupies RH and
replaces only the right star, leaving the left star intact; unsupported LH
selection is rejected. Skin recoloring/reset and changing the face retain blush.
The production build and hosted-origin deployment have not been run for this
follow-up. Previously documented unrelated whole-project lint/test failures
remain outside these focused results.

## Gelo character preview, local only

User review on 2026-09-06 accepts Gelo's current appearance for now. Shader
differences remain visible and unresolved; this acceptance does not establish
client shader parity. Shader refinement is a follow-up, not a blocker for this
checkpoint. The user authorized committing the implementation and updated plans.
Deployment remains separately authorized.

Commit checkpoint verification: 31 C# and 11 Python tests pass. Frontend checks
pass with zero errors/warnings, 27 focused logic tests, nine acceptance-06
asset tests and 133 binding tests against the Gelo preview library. The
project-wide lint command fails on formatting in 62 files outside this change,
including `tests/housing.test.ts` and `tsconfig.json`; its Prettier configuration
also warns about the obsolete pluginSearchDirs option. No repository-wide
formatting was applied. The earlier production build predates the Gelo importer.

Read Gelo's saved appearance from local `tria-game-server` without database
writes. Open `http://127.0.0.1:4000/outfits?preview=gelo-01` in the existing
development server. The isolated `character-previews/gelo-01` library loads
eight exact appearance IDs: 10200124, 10300135, 11320024, 12220024, 11620024,
11720024, 11220024 and 10500001, with saved skin, eye and equipment colors.
Client itemdata confirms hair 10200124 inherits preset 10200096. Its authored
C form fits the beret. Skin matches client palette 1 entry 12.

The page explicitly omits back sign 11820024 because its skinned attachment
is unsupported, blush 10400108 because facial decals are unsupported, and
both Fire Prism Stars 13400306 because separate hand instances and their
source offsets are unsupported. Sparkles and badge effects remain excluded.
No replacement equipment or body resizing was used.

T3 front, side and back views were inspected at fitting_idle_a time 0.3.
Visible-tab playback advanced from 0.2571 to 4.2626 seconds with all eight
items equipped. Local captures are under `NifToGltf/obj/gelo`; recording is
`C:/Users/atade/.t3/userdata/browser-artifacts/browser-recording-mtqfbr62.webm`.
Eleven new GLTFs validate without errors or warnings; eleven targeted native
binding tests and eighteen focused frontend tests pass. Svelte check reports
zero errors and warnings. These checks do not establish missing-item support.
All 552 original release-02 inventory hashes remain unchanged. This preview
does not expand the reviewed publishing library and is enabled only in dev.

## Simulator release candidate, local review

The fixture dropdowns have been replaced by a real searchable catalog at
`/outfits`, joined to the local database without schema or data changes. The
versioned candidate `simulator-release-02` has 124 models and 112 item/body
catalog entries: 46 reviewed, 54 preview and 12 unavailable. The default API/UI
returns 23 reviewed items per body. Available-model browsing returns 47 named
items per body after database gender/name filtering. Native success and visual
acceptance are separate statuses.

Implemented equipment transactions, multi-slot conflicts and gender-specific
cutting, source C/D hair variants, hair length restoration, four source face
presets, exact blink timings, source palettes, shared skin and three own client
backgrounds. Selected cap combinations have been rendered on both bodies.
Direct T3 review found and removed an unsupported hardcoded Sad option. The
three exported choices are Blink, Happy and Angry. Source facial expressions
and body poses are independent viewer choices.

T3 checks cover male and female hoodie sets, leather sets and robes, both face
choices per body, and all six clips at 0.75 seconds. Earlier front/side/back
hoodie views used fitting_idle_a at 0.3 seconds. Core IDs include 11400350 male,
11400158 female, 11400003/11500003 leather, 12200001 CL+PA robe, 11300001/2 caps,
10200230 male hair, 10200224 and 10200009 female hair, 10300001–4 faces,
11200001/2 earrings, 11600001/7 gloves, 11700001/3 shoes, 11800008/9 back gear,
13000001 RH, 14000002 LH, 13100068 OH and 15500002 paired hand equipment.
Male 10200001 was also reviewed loose; a hat pairing fails explicitly.

Interaction evidence:

- Full robe clears CL+PA; a separate garment removes the complete robe.
  Paired hand equipment clears RH+LH. Other slots survive these changes.
- A missing second robe GLTF returns HTTP404 and leaves previous equipment
  unchanged. Unsupported hair/hat combinations also preserve it.
- Eight cap round trips returned to 26 geometries / 65 textures. Hair dye and
  loose morph values 0.3/0.5 survive fitting and removal. This bounded check
  does not prove unlimited-session memory behavior.
- UI edits and reset work for skin, eyes, robe and hair. Garments inherit skin.
  Source palette IDs drive garment choices. Eye reset uses source palette 3
  entry 0 as an explicit viewer default; client character-creation defaults are
  not claimed. Cap transforms are not guessed or applied globally.
- Blue sky, Ellinia and Henesys load from our Image archive exports. Save image
  produced a valid 823x520 PNG, 674131 bytes, with selected dyes, expression and
  Henesys. The resulting image was decoded and visually inspected.
- At 390px, document width is 390px and no main content overflows. Both verified
  catalog pages load. Invalid pagination returns 400; literal injection-shaped
  and wrong-body searches return no rows.

Evidence: `Diagnostics/simulator-review.json` and local
`NifToGltf/obj/research/simulator-*Matrix.jpg`, `simulator-*Expressions*.jpg`
and `simulator-exportPreview.jpg`. Approval covers recorded combinations and
sampled states, not every possible combination or frame. Source root motion
remains intact. Happy/Angry hold the final source frame independently of poses.
Other expressions and combat poses are absent. The four tested cap/hair pairs
are listed in the review, and other pairs are not appearance-certified.

Final-package matrices were recaptured with preserved aspect ratio. The two
full-size examples are `simulator-finalFemale.jpg` and `simulator-finalMale.jpg`.
Live run playback advanced the female mixer from 16.0611 to 19.4 seconds and
the male mixer from 0.75 to 4.2556 seconds, with changed foot world matrices.
The female recording is `simulator-female-playback.webm` in the same evidence
directory. All-item browsing showed Heart Earrings 11200003 as unavailable
with a disabled equip button and an explicit explanation.

Reference checks on https://hunya.duckdns.org: the male selector reports that
male characters are unsupported. Female 10200224 + 11300001 retains an A hair
URL with hair-cut flag 8 and zero cap offsets. Reference canvas readback was
black, so reference silhouette parity is unresolved. Robe 12200001 selected
12220001_F_ProbationMage and cleared PA to -1/null, matching our XML CL+PA
definition. Saved reference cap colors were (63,59,51) and (20,18,15); saved
hair dye persisted on selection. These session colors do not establish defaults.
Reference expression interaction gave no reliable visual result. Our common
emotion imports, exact frames and local rendering independently establish the
supported expressions. No reference assets are shipped.

Verification: 31 C# tests with source fixtures, 11 Python tests, 156 focused
frontend tests, zero typecheck errors/warnings, and a successful production
build. All 124 GLTFs validate with zero errors and warnings.
`build_simulator.ps1` rebuilt from the extracted client resources and reproduced
all 552 inventory hashes exactly. The native candidate batch separately reports
116 conversions, zero missing inputs and 12 rejected parts; the hair batch adds
eight conversions. Face and background batches report 206 and three texture
conversions respectively.

Local release URLs for both bodies, cap hair, face pixels, metadata and background
return 200 with correct MIME types, ETags, CORS `*` and development `no-cache`.
Publishing must keep the versioned `simulator-release-02/` prefix immutable,
set production cache headers and verify the actual hosted origin after approval.
The CDN configuration and production flow have NOT been changed or tested.

Full non-effect replacement remains incomplete. Native remains opt-in;
`--noesis` selects the existing default. Source changes are being committed at
the user's request. Nothing was deployed, production data was not modified,
and existing processes were not stopped. Generated libraries and Gelo's saved
appearance remain local ignored artifacts; they are not included in Git.

The implemented scope follows [SIMULATOR-PLAN.md](SIMULATOR-PLAN.md). The existing
unrelated nested-test failure in `unescapeHtml.test.ts` remains documented;
the focused pass is not a clean whole-project suite. Wider NPC/map converter
findings below remain valid and do not block this supported simulator candidate.

## Clothing preview correction

The user reported the female CL appearance was wrong. The selected 11400182
NIF contains an untextured test mesh without a UV stream. It was an unsuitable
clothing acceptance example. Neither installed Item archive contains the old
11400040 female event shirt, so the current KMS2 female hoodie 11400158 was
extracted to `NativeSources/Clothing-02` and selected instead.

The hoodie also exposed a conversion bug: itemmodel selfnode CL selection
discarded the sibling CL_Skin mesh. CL replacement now retains CL_* source
parts, and the viewer hides the naked body's CL_* parts while clothing is
equipped. Both clothing plan entries now read itemmodel/114.xml.

Acceptance-05 exports all twelve models with zero glTF errors/warnings.
Thirty-one C# tests and 24 focused frontend tests pass, with zero frontend
typecheck errors/warnings. The new source regression checks that both hoodie
meshes and their geometry survive grafting; the frontend checks replacement,
restoration and the real two-mesh garment deformation. T3 canvas capture shows
the blue hoodie with intact sleeves and exposed skin. The male shirt was also
rendered after this change. Earlier captures of the yellow test mesh do not
establish clothing appearance acceptance.

The user then flagged the male shirt's darker arms. CL_Skin retained the
garment's tan override defaults while the male body used a lighter pink palette.
The outfit consumer now owns one skin palette from the selected body and applies
it to equipped MS2CharacterSkinMaterial parts. Skin edits and reset affect both
body and equipment; fabric dyes stay independent. Three additional regressions
bring the focused frontend suite to 27 passing tests. Typecheck still has zero
errors/warnings. T3 captures verify matching default skin and a shared blue
skin edit, followed by reset.

The next report concerned the arm/hand width discontinuity. The staged
11400040 shirt's exposed-arm mesh does not meet the selected body's wrist
opening. Bare CL and GL have sixteen coincident seam vertices; the shirt
fails a seam regression at the first checked vertex with a 0.0202467-meter
gap. The source and grafted joint transforms have unit source-space scale.
This is an incompatible garment/body shape, not a reason to guess a wrist scale.

The current-client 11400350 item uses 11400151_m_hoodsleeveless.nif, extracted
to `NativeSources/Clothing-03`. Its exposed arms meet all sixteen body seam
vertices within 0.00001 meters at 0, 0.3 and 0.75 seconds of fitting_idle_a.
The same test deliberately failed against Acceptance-05's staged shirt and
passed against Acceptance-06. The acceptance plan and local preview now use
this compatible male garment. The staged 11400040 shirt remains incompatible;
its geometry was not altered to conceal that mismatch.

Acceptance-06 exports twelve models with zero glTF errors/warnings. All 28
focused frontend tests pass and typecheck reports zero errors/warnings.
T3 capture shows the current-client male garment with connected wrists and
the shared skin palette. It is left equipped with idle playback selected.

Reproduce the added source with:

```powershell
dotnet run --project NifToGltf.Archives -- 'D:/MS2/KMS2 Debug/Data/Resource/Model/Item.m2d' '11400158_f_hoodtshirts.nif$' Maple2Storage/Resources/NativeSources/Clothing-02
dotnet run --project NifToGltf.Archives -- 'D:/MS2/KMS2 Debug/Data/Resource/Model/Item.m2d' '11400151_m_hoodsleeveless.nif$' Maple2Storage/Resources/NativeSources/Clothing-03
```

The extraction destination must be empty. Copy the same client's
`Xml/itemmodel/114.xml` to `NativeSources/Xml/itemmodel/114.xml` before running
the acceptance plan. Existing extracted sources do not need to be overwritten.

## Implemented and checked

- DDS RGB masks and embedded pixels, sampler modes, texture UV matrices,
  triangle strips, generated tangents, software skin palettes and relative
  position morphs. The C# fixture suite has 30 passing tests; Python has nine.
- Default material colors use the ColorOverride fragment from the installed
  KMS2 generated HLSL. Alpha blending takes precedence when the source also
  enables testing. Control masks retain their spatial resolution. Specular
  lighting and face replacement/animation are not established by this bake.
- Both player archives are located. Six curated clips per gender export
  successfully, including 30.1.0.3 walk/run KFs. Extracted body SHA256 values
  equal the staged body files. Source animation tracks remain unchanged.
- Animated batch plans, explicit effect exclusions and portable asset manifests.
  Failed selected clips prevent writing that model instead of silently vanishing.
- Client `Xml/itemmodel` provides per-item/gender attachment evidence. Examples:
  CP to Head, EA to Head, hair replacing HR under Head, a cape to Spine1 and
  a dagger to Weapon_Side_L_Point. These are not universal slot rules.
- The actual item/NPC viewers select native manifest assets and named merged
  clips without applying the legacy orientation. `/outfits` shares the body
  skeleton and exposes body/equipment selection, playback and image capture.
- Twelve acceptance exports validate with zero errors/warnings. Twenty-two frontend
  tests pass, including eight comparisons of real equipment deformation against
  an independently animated skeleton at three timestamps. These cover both
  body variants, hats, clothing, hair, earrings, a cape and a weapon.
- The T3 actual NPC route selected `native-acceptance-03/npc/rabbit.gltf`,
  reported `loaded=true`, exposed thirteen clips, and accepted a 0.5-second
  seek. T3 reports the tab hidden; snapshots failed and canvas capture did not
  provide a usable rendered image. The user reported idle playback in their
  preview. This does not verify the new material or outfit appearance.
- Later T3 canvas capture succeeded on `/outfits`. The female body renders with
  tan skin, hat and clothing follow the fitting pose, and changing/resetting a
  torso color works through the UI. These captures are in `obj/research`.
  They establish the exercised flow, not full client appearance parity.
- T3 also loaded the male body with hat, top, hair, earrings and dagger and
  captured the fitting pose without runtime errors. The hair intersects the hat.
  Item 10200230 declares scale values `0.3,0.7,1` and a `customize/capTransform`
  position/rotation. Their client application rule has not been implemented.
  This combination fails appearance acceptance despite passing deformation tests.
- Outfit color controls use the exported original diffuse/control maps and client
  formula. A browser pixel comparison matches the converter on seven opaque body
  materials. The transparent FA material differs by up to three byte values after
  canvas decoding. Lossless transparent recoloring still needs verification.
  Five focused tests cover color sampling, alpha, mask resolution and independent
  editable texture sources. The initial shared-source texture bug is corrected.
- Acceptance-04 contains the explicit color-control texture metadata and all
  twelve files validate without errors/warnings. The scoped Batch-05 below
  predates that metadata-only addition. A fresh thirteen-clip rabbit comparison
  from Acceptance-03 reproduces the recorded interpolation differences.

The all-input Batch-04 run produced 2,620 converted, 9 missing and 201 failed
out of 2,830. All 2,620 glTFs validate with zero errors/warnings. It preceded
the final texture recovery and effect exclusion run. Keep its counts separate
from the scoped report in `Diagnostics`.

The scoped Batch-05 run produced **2,626 converted, 1 effect-excluded, 2 missing
and 201 failed**. All 2,626 converted files validate with zero errors/warnings.
Of the original 471 rejections, 267 now convert, 1 is effect-excluded, 2 are
missing and 201 still fail. All original successful inputs remain converted.
[non-effect-results.json](../Diagnostics/non-effect-results.json) records every
remaining rejection and missing source with portable paths.

Frontend `pnpm check` passes with zero errors/warnings. The focused native
tests pass. A broader Vitest run passes 41 tests and fails the existing
`tests/unescapeHtml.test.ts` wrapper because it nests `test()` calls. The same
wrapper was confirmed in HEAD and was not changed. Existing Prettier configuration
also emits an ignored `pluginSearchDirs` warning. These are separate from the
native conversion acceptance failures.

## Source recovery and evidence

Archives searched: Character, Textures, Effect, Map, Item and Npc under both
`D:/MS2/KMS2 Debug/Data/Resource/Model` and
`D:/Projetos/MapleStory2/MapleStory2Client/Data/Resource/Model`.
Recovery uses dedicated directories; source archives and staged models remain
unchanged. `NifToGltf.Archives` provides a repeatable read-only extraction CLI.
The new inventory scans all external texture references, not only first errors.

Only four referenced texture names remain absent from the inspected staged and
recovered roots, and the six searched archives in each installed client:

- `02020001_M_OrangeMushroom_Body_N.dds`
- `02020001_M_OrangeMushroom_Body_S.dds`
- `02020001_M_OrangeMushroom_Face_01.DDS`
- `Eff_perform_FrenchHorn_A01.dds`

The first three belong to `Npc/02/05/02059990/02059990_m_orangemushroomtest.nif`;
the last belongs to `Effect/bg/perform/eff_perform_frenchhorn_a.nif`.
Other exported client versions or the original authoring textures are needed.
Conflicting same-basename textures also remain explicit failures.

Material sources are under the debug client's
`Data/Shaders/Current/MS2Character{Skin,Hair}Material` directories.
`Xml/itemmodel/103.xml` selects the face NIF and `emotion/item/10300003.xml`
imports FemaleCustom with itemCode 00300003. `emotion/common/femalecustom.xml`
contains per-expression texture/control frame sequences. Face NIFs and the
00300001/00300003 texture sets were extracted into NativeSources for this work.
They are not yet connected to the runtime material animation system.

`Data/Shaders/Current/MS2StandardMaterial/Shader0021-P.hlsl` also shows the
reflection path: projected world reflection coordinates sample EnvironmentMap,
then gloss RGB modulates the reflection. The source binding of the world
projection matrix remains unresolved; exporting a generic PBR reflection would
not establish the same appearance.

Relative morph interpretation was checked against the
[NiMorphMeshModifier reference](https://github.com/sigmaco/gamebryo-v3.2.0.661/blob/master/Documentation/Reference/NiMesh/NiMorphMeshModifier.htm):
target zero is the base and later relative targets contain incremental offsets.

## Remaining required work

1. Implement visible reflection and billboard behavior in an appropriate glTF
   consumer. Batch-04 has 152 NiTextureEffect and 38 NiBillboardNode first
   failures. Sampled texture effects are enabled sphere reflections using
   ReflectionMap_09.DDS. Discarding them would lose non-effect appearance.
   Two camera nodes, four geometry-free files, one invalid texture reference,
   one zero normal, one nonfinite transform and two ambiguous textures also fail.
2. Complete source-driven face selection, expression/material animation,
   lossless transparent dyeing, normal/specular behavior and render comparisons.
   Color baking and a validator pass do not establish character fidelity.
3. Validate TCB quaternion interpolation for Balrog Attack_01_G, Attack_01_H,
   Attack_02_G and Attack_02_H. Their neutral TCB parameters still describe
   nonconstant quaternion curves. Both inspected Noesis installations returned
   “Detected file type: Unknown” for a paired KF reference export; no new
   reference was produced. A working reference exporter or client curve
   implementation is needed to resolve timing/tangent conventions.
4. Establish the client's initial duplicate-sequence selection rule.
   Attack_Idle_A has two same-named roots with 102 evaluators and different
   durations. Female sit_chair_idle_a also has two roots. ActorManager's
   documented reload replacement behavior does not establish initial selection.
5. Handle equipment with private animated bones and mixed rigid/skinned parts,
   hair/hat fitting, source customization palettes, remaining attachment
   modifiers and body cutting behavior. The tested
   shared-skeleton path rejects unknown bones instead of silently pinning them.
6. Exercise the representative models visibly in the actual viewers, including
   animation and root motion, then rerun the retained batch and acceptance gate.
   Keep Noesis as the explicit fallback until these checks pass.

Effects and particle simulation remain excluded. The one explicit whole-file
exclusion is the standalone random-box reveal effect, with source evidence in
`Diagnostics/scope-exclusions.json`. Instruments, lift-up objects and cape
meshes remain required even when their source directory is named Effect.
