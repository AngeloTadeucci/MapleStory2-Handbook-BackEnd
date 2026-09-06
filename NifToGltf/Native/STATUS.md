# Non-effect replacement status, 2026-09-06

Replacement is not ready. Native remains opt-in; `--noesis` explicitly selects
the existing default. Nothing was committed or deployed in this continuation.

The next implementation priority is [clothing simulator completion](SIMULATOR-PLAN.md):
catalog, equipment rules, hair/face customization, backgrounds/visual checks,
then publishing. Individual missing assets are acceptable under the revised user
scope. Full NPC/map conversion is not a simulator release gate; the unresolved
converter findings below remain valid.

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
