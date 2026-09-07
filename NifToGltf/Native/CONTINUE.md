# Continuation prompt: replace Noesis for non-effect assets

## Current priority, updated 2026-09-06

All four authorized follow-ups are implemented and locally verified: sign KF
animation, star combat idle/run and draw/stow, broader clothing compatibility,
and source half-Lambert/gloss/specular shading. The default library is
`simulator-release-04`, with 144 models and 82 reviewed item/body entries.
`/outfits?preview=gelo-05` loads Gelo with twelve saved instances and dyes.
Read the latest STATUS.md section for exact states, tests and remaining limits.

The 620 release files reproduce byte for byte using Diagnostics/refine_simulator.py
and motion-library-review.json. Paired star back placement overlaps at the XML
anchor and remains explicitly unresolved. Full client scene/rim/hair shader
parity and cape cloth physics are not established. Knuckles now use source
hand attachnodes; their old back placement was not correct appearance evidence.
Effects and particle simulation remain excluded. Complete NPC/map conversion
and global Noesis replacement are not simulator release prerequisites.

Next action is review of this concrete local release before separately authorized
publishing. Do not restart the completed implementation phases or claim every
preview item is verified. The user authorized committing these follow-ups on
2026-09-06; deployment remains unauthorized. Preserve local commits, ignored
asset releases, the read-only DB appearance snapshot and existing processes.
Both repositories were fetched at the start: master, three ahead and zero behind.
The source commits do not contain the generated libraries or character snapshot.
The production build passes and packages only simulator-release-04 under gltf.
All 620 packaged files match inventory hashes. Local character snapshots and
research libraries are excluded without deleting their source files. See STATUS.md
for the corrected Vite import restriction and final mobile/export evidence limits.

The original converter prompt below is a wider backlog, not a release checklist.

## Original replacement prompt

Implementation checkpoint: [STATUS.md](STATUS.md) records the work executed
from this prompt, current tests, recovered sources and remaining blockers.
The requirements below describe full replacement, not a completed checklist.

Continue implementing the native MapleStory 2 NIF-to-glTF converter until it
can replace Noesis for characters, equipment, NPCs and ordinary map geometry.
Complete the remaining conversion and frontend integration work. Effects and
particle simulation are explicitly out of scope. Do the implementation and
verification, not just a new plan.

Repositories on this Windows machine:

- Backend: `D:\Projetos\MapleStory2\Maple2_Codex\MapleStory2-Handbook-BackEnd`
- Frontend: `D:\Projetos\MapleStory2\Maple2_Codex\MapleStory2-Handbook`

Read each repository's AGENTS.md and inspect current status and upstream before
editing. Preserve unrelated changes. Use `py` and `pnpm`. Do not deploy, modify
production data, or commit further changes unless asked. The user authorized
running the local preview server in this conversation; check whether port 4000
is already serving before starting another process. Never kill user processes.

Read first:

- Frontend `plans/09-nif-to-gltf-converter.md`, if present locally. This file is
  ignored by Git; it is not guaranteed to exist in a fresh checkout.
- Frontend `plans/08-outfit-simulator.md` for the consumer's requirements.
- Backend `NifToGltf/Native/README.md` and the implementation in that directory.
- Backend `NifToGltf/Diagnostics/README.md`, `batch-results.json`,
  `format-survey.json`, and `rabbit-animation-comparison.json`.
- Backend `NifToGltf.Tests/Program.cs` and frontend
  `src/routes/dev/nif-converter/`.

## Starting point

Native conversion is opt-in through `--native`. The existing Noesis command is
still the default. Native code handles the nine observed stream formats,
NiNode/NiMesh hierarchy, palettes and skins, DXT1/3/5 textures, basic materials,
full named skeleton grafts, KF/KFM selection and merged transform clips.
Batch conversion currently produces static models only.

The recorded full static run exported 2,359 of 2,830 NIFs and rejected 471.
The successful outputs had zero Khronos validation errors and three files
with missing-tangent warnings. Rejections record only the first failure per
file. These totals include effects and must be reclassified for the new scope.
Do not count excluded effects as conversion successes.

Nineteen C# tests and six Python scanner tests passed. All eight development
preview files passed glTF validation without errors or warnings. Body/hat,
Balrog walk and rabbit run playback were exercised in the browser. This does
not establish full-library appearance or animation fidelity.

Local ignored assets and QA outputs:

- `Maple2Storage/Resources/Models`: source NIF, KF, KFM and DDS files.
- `Maple2Storage/Resources/Models/Textures`: additional texture lookup root.
- `Maple2Storage/Resources/NativeBatch-02`: the completed static batch and full report.
- `Maple2Storage/Resources/NativeProof`: individual proof exports.
- `Maple2Storage/Resources/GLTF/21000174_m_rabbitdollcymbalsgrey`: existing Noesis
  reference geometry and thirteen separate animated exports.
- `NifToGltf/obj/research`: validator dependency, detailed validation reports,
  source specification and screenshots from the investigation.
- Frontend `static/gltf/native-proof`: preview fixtures, also ignored.
- Local proof route: `http://127.0.0.1:4000/dev/nif-converter`.

## Required work

1. Reclassify the 471 rejected inputs. Separate effect-only files from required
   non-effect assets. Handle remaining geometry, skinning, embedded textures,
   UV transforms, DDS formats, mesh modifiers, scene wrappers and primitive
   types needed by the retained assets. Investigate the three tangent warnings.
   Of the original failures, 127 requested textures were not found beside the
   model or under the supplied texture root. Search available client exports
   and extraction paths before concluding those assets are unavailable.

   Exclude effect-only assets and particle behavior. If a character, NPC or item
   contains an effect attachment, preserve and convert its ordinary geometry
   and skeleton where separable, and report exactly what effect content was
   omitted. Do not exclude an entire NPC just because it references an effect
   node. Do not treat every NiTextureEffect or billboard as disposable without
   checking its role in the asset's visible appearance.

2. Fix character material behavior using source data and client/reference
   evidence. The body preview has overlapping facial atlas details and
   uncustomized white skin. Implement correct face selection, skin color,
   dye/control masks, normal/specular behavior, alpha and texture transforms
   needed for the models. Required face/material animation remains in scope
   even though effects are excluded. Avoid guessed UV or color adjustments.

3. Finish animation support for retained assets. Balrog's 65-entry KFM rejects
   four TCB quaternion clips: Attack_01_G, Attack_01_H, Attack_02_G and
   Attack_02_H. Attack_Idle_A.kf contains two same-named NiSequenceData roots,
   each with 102 evaluators but different durations. Establish the client's
   sequence selection rule instead of choosing a root arbitrarily. Support
   other required interpolation types and verify root motion.

   The rabbit's thirteen clips are merged already. Twelve agree with the
   existing Noesis keys within 0.000768 degrees and 0.000239 source units.
   Idle differs by 1.243 degrees on Point01 because native preserves quadratic
   Euler curves while Noesis interpolates between key rotations. Do not force
   reference parity by discarding source interpolation behavior. Float32
   timestamp deduplication is required and has regression coverage.

4. Locate or extract the player KF set referenced by the 1,995-entry animation
   index. The full set was not located in the previously inspected tree.
   Choose a curated set rather than embedding all 1,995 clips. A compatible
   fitting_idle_a from Npc/11/11000148 currently proves body/hat animation.
   Verify both body variants, skinned clothing, rigid gear, attachment offsets
   and motion. Only CP to Bip01 Head is currently inferred. Derive the other
   attachment mappings from evidence, including hair, earrings, capes and
   weapons, rather than guessing bone names.

5. Add animated batch conversion with explicit per-model clip selections and
   equipment skeleton/attachment metadata. Produce a portable manifest showing
   converted assets, excluded effects, missing assets and real failures.
   Preserve one mesh/material set per model with named clips in its animations
   array. Keep source assets intact and use dedicated output directories.

6. Integrate native output into the actual Handbook model viewer and the
   equipment/skeleton consumer contract from plan 08. A development proof page
   is not sufficient. Preserve exact bone names, including legitimate helper
   names such as SATA9NI_Bone01. Native output already converts Z-up centimeters
   to Y-up meters; do not apply the legacy Noesis rotation or scale again.
   The proof composer keeps separate skeletons and is not the production
   implementation for sharing a skeleton between body and equipment.

7. Make native conversion the default for the verified non-effect workflow
   once acceptance checks pass. Keep Noesis available as an explicit fallback
   for excluded effects and unresolved assets during migration. Do not remove
   the existing path prematurely. Update the documentation and plan to explain
   the resulting default, fallback selection, supported scope and remaining
   asset gaps. Production deployment is a separate step requiring authorization.

## Verification and completion criteria

Run the focused regression suite after relevant changes:

```powershell
dotnet run --project NifToGltf.Tests -- Maple2Storage/Resources/Models/Character/female/f_body.nif
py -B -m unittest discover -s NifToGltf/Diagnostics -p 'test_*.py' -v
```

Use the existing `validate_gltf.cjs`, `compare_animations.py` and
`summarize_batch.py` diagnostics. Install the optional validator under the
ignored research directory only if needed:

```powershell
pnpm --dir NifToGltf/obj/research add gltf-validator
```

Run frontend `pnpm check`, relevant lint/format checks and real browser flows.
Do not edit a checkout while its test suite is running. Add numerical
regressions for each new decoding/interpolation rule and exercise visuals for
materials, skinning and playback. Existing screenshots are historical evidence,
not verification of new edits.

Before declaring replacement ready, demonstrate representative bodies,
equipment, NPCs and map geometry with correct materials, attachments and
animation in the actual viewer. Re-run the retained batch with zero glTF errors
and explain any remaining warnings. Report converted, effect-excluded and
blocked counts separately. Structural validation alone is insufficient.

Persist through all actionable work in scope. If missing source assets or
unavailable client evidence block a requirement, identify the exact file or
rule needed, document the searches and evidence, and continue independent work.
Do not label an unresolved non-effect requirement complete or hide it as an
effect exclusion.
