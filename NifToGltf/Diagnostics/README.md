# Plan 9: format survey and conversion verification

The scan on 2026-09-06 disproved the original four-format assumption. The user
authorized continuing with the corrected nine-format inventory. Native body
conversion, skeleton grafting, merged animation and a static batch are now
implemented. Noesis remains the default. See the [native converter](../Native/README.md)
for commands, verification results and the remaining fidelity requirements.

## Reproduce

### Supported simulator release

The local candidate is
`../MapleStory2-Handbook/static/gltf/simulator-release-02/`. It includes portable
models, exact item/body catalog, customization, face pixels, backgrounds,
`coverage.json`, `appearance-review.json` and a SHA256 inventory. There are 46
reviewed item/body entries, 54 previews and 12 unavailable entries. Conversion
alone never sets the reviewed status.

Run from the backend root with the existing extracted client resources:

```powershell
NifToGltf/Diagnostics/build_simulator.ps1 -Output NifToGltf/obj/simulator-reproduced-release -Work NifToGltf/obj/simulator-reproduce-01
```

That exact command was exercised and reproduced all 552 inventoried files.
Choose new output/work paths on later runs; existing directories are preserved.
The script does not publish. `simulator-review.json` pins the inspected catalog,
model bytes and customization textures. Changed inputs fail the review gate;
do not replace its hashes merely to make a build pass. Render changed assets
and record a new review/version first.

Required source layout under `Maple2Storage/Resources/`:

- `Models/Character/{male,female}/` and `Models/Textures/`: our existing body,
  six-clip and DDS exports from the installed client.
- `SimulatorSources/Xml/`: itemmodel XML, emotion imports/common sequences,
  colorpalette.xml. The client source is `D:/MS2/KMS2 Debug/Data/Xml.m2d`.
- `SimulatorSources/Item/`: exact item-model paths selected by
  `simulator_library.py prepare` from `Resource/Model/Item.m2d`.
- `SimulatorSources/HairForms/`: eight exact C/D NIFs listed in
  `simulator-hair-plan.json`, from the same Item archive.
- `SimulatorSources/Face/item_face/`: the extracted source face DDS textures.
- `SimulatorSources/Background/bg/`: bg_blue.dds, bg_henesys_a.dds and
  bg_ellinia_a.dds from `Resource/Image.m2d`.

The archive helper's list-only mode creates the required exact-path NIF index:

```powershell
dotnet run --no-build --project NifToGltf.Archives -- 'D:/MS2/KMS2 Debug/Data/Resource/Model/Item.m2d' '\.nif$' | Set-Content NifToGltf/obj/research/simulator-item-index.json
```

Extraction requires an empty destination and writes path/length/SHA256 receipts.
Do not overwrite existing client archives. `prepare` writes the candidate batch
plan and extraction regex. Missing sources, ambiguous URNs and unsupported
native parts remain explicit failures. The candidate native batch's nonzero
exit is expected for its twelve rejected parts; the release gate separately
requires every approved item to exist unchanged.

Verification used:

```powershell
dotnet run --project NifToGltf.Tests -- Maple2Storage/Resources/Models/Character/female/f_body.nif
py -B -m unittest discover -s NifToGltf/Diagnostics -p 'test_*.py'
node NifToGltf/Diagnostics/validate_gltf.cjs ../MapleStory2-Handbook/static/gltf/simulator-release-02 NifToGltf/obj/simulator-06/release-validation.json
```

In the frontend root:

```powershell
$env:SIMULATOR_LIBRARY_DIR='static/gltf/simulator-release-02'
$env:NATIVE_ACCEPTANCE_DIR='static/gltf/native-acceptance-06'
pnpm exec vitest run tests/nativeAssets.test.ts tests/sharedSkeleton.test.ts tests/nativeOutfitAssets.test.ts tests/bodyVisibility.test.ts tests/materialColors.test.ts tests/skinColors.test.ts tests/outfitCatalog.test.ts tests/simulatorLibrary.test.ts
pnpm check
pnpm build
```

Results: 31 C#, 11 Python and 156 focused frontend tests pass; 124 GLTFs have
zero validator errors/warnings; typecheck and production build pass. The local
build log is `NifToGltf/obj/research/simulator-production-build.log`. Existing package/chunk warnings and
the unrelated nested `unescapeHtml.test.ts` failure do not represent a clean
whole-project suite. Direct T3 outfit, expression, dye, background and PNG
evidence is recorded in the review and [STATUS.md](../Native/STATUS.md).

For later authorized publishing, upload this one versioned directory under the
configured model URL and deploy the matching frontend build. Serve JSON as
application/json, GLTF as model/gltf+json and images with their image MIME type.
Use CORS for the Handbook origin and immutable caching for this versioned
prefix. Never overwrite it in place. Local responses were checked; actual CDN
headers, deployed configuration and production outfit/PNG flows remain untested
until publishing is authorized. Other Noesis, NPC and map workflows remain as
they were.

### Converter diagnostics

From the backend repository root on Windows:

```powershell
py -B -m unittest discover -s NifToGltf/Diagnostics -p 'test_*.py' -v
py -B NifToGltf/Diagnostics/scan_nif.py Maple2Storage/Resources/Models --output NifToGltf/Diagnostics/format-survey.json
```

Use `python3` instead of `py` on Linux. No third-party dependencies are needed.
Exit 0 means the format survey agrees with the original four-format table.
Exit 1 means a parse failure or no input. Exit 2 means additional formats were
found. This survey does not establish that the complete step 0 gate passes.
It never changes source assets or invokes Noesis.

## Results

All 2,830 NIFs passed header, string-length, block-boundary, stream-payload,
stride, region-boundary, streamable-flag, and footer checks. The scan inspected
28,161 streams. See [format-survey.json](format-survey.json) for exact counts,
example files, block types, metadata lengths, and failures.

The directory totals are Character 2, Effect 108, Item 385, Map 39, Npc 2,295,
and Textures 1. The last file accounts for the discrepancy between the plan's
directory table and its total.

| Format | Storage | Component occurrences | In original table |
|---|---|---:|---|
| `0x00010215` | uint16 x1 | 16,098 | Yes |
| `0x00010425` | uint32 x1 | 206 | No |
| `0x00010435` | float32 x1 | 13 | No |
| `0x00020436` | float32 x2 | 7,765 | Yes |
| `0x00030437` | float32 x3 | 38,525 | Yes |
| `0x00040108` | uint8 x4 | 5,756 | Yes |
| `0x00040110` | normalized uint8 x4 | 31 | No |
| `0x00040214` | int16 x4 | 12 | No |
| `0x00040438` | float32 x4 | 137 | No |

Counts refer to component declarations, not vertices or files. Storage formats
alone do not identify positions, weights, colors, or indices. That requires the
semantic bindings in each `NiMesh`.

## Corrections to the plan

The [niftools specification](https://github.com/niftools/nifxml/blob/develop/nif.xml)
documents the nine storage types above and these layout details:

- Bits 16 through 23 hold component count. Bits 8 through 15 hold bytes per
  component. The plan reverses those descriptions, although its table sizes
  are correct.
- The u32 after block count is a metadata byte count, followed by that many
  bytes. It is not an always-zero field. The report lists files with metadata.
- `NiDataStream` has one boolean `Streamable` byte after its payload.
- Encoded stream type names contain `0x01` separators. Treating
  `NiDataStream\x011\x0118` as a separate object class inflates the class count.

The scan found 84 encoded type names, or 73 classes after collapsing stream
variants. The estimate of roughly 20 types does not describe this staged
library. Particle systems, morphs, physics, texture controllers, and embedded
pixel data are present. Presence alone does not establish which are required
for the body proof or how much work faithful full-library conversion needs.

## Implemented revision

The native reader uses all nine storage decoders and the semantic bindings in
each mesh. The body exported and rendered in the Handbook viewer. Its total is
2,034 vertices and 2,762 triangles. `CL_Skin` has 317 vertices and 434 triangles;
the original plan's 466 vertices and 480 triangles describe the `GL` hand mesh.
Named helper bones are retained, including the body's `SATA9NI_Bone01`.

Six unit tests exercise metadata, masked type indices, format-gate failure,
stream truncation, stride and region errors, trailer validation, invalid
footer references, block type bounds, and empty input. They validate the
survey only. They do not validate geometry, skinning, materials or animation.

## Native validation and reference comparison

The continuation's current scoped results are in
[non-effect-results.json](non-effect-results.json): 2,626 converted, one explicit
effect exclusion, two missing assets and 201 failures. All converted outputs
have zero validator errors/warnings. The older `batch-results.json` is retained
as the baseline of 2,359 conversions and 471 rejections.

`inspect_nif.py` exposes named scene blocks and texture references for source
inspection. `texture_inventory.py` scans every external texture reference;
it does not stop after the first missing material. `create_scope_plan.py` adds
only the evidenced exclusions in `scope-exclusions.json`. These diagnostics
have nine passing Python tests in total with the original scanner tests.

```powershell
py -B NifToGltf/Diagnostics/texture_inventory.py Maple2Storage/Resources/Models --textures Maple2Storage/Resources/Models/Textures Maple2Storage/Resources/NativeSources Maple2Storage/Resources/NativeRecovery-05 --output NifToGltf/obj/research/texture-inventory.json
py -B NifToGltf/Diagnostics/create_scope_plan.py Maple2Storage/Resources/Models --output NifToGltf/obj/research/scope-plan.json
```

The optional `NifToGltf.Archives` CLI uses the existing Maple2.File parser
package to read installed M2D/M2H pairs. It never overwrites an extraction
directory. Omit the output argument to list matching entries without writing:

```powershell
dotnet run --project NifToGltf.Archives -- 'D:/MS2/KMS2 Debug/Data/Resource/Model/Character.m2d' '(female|male)/(idle_a|fitting_idle_a|walk_a|run_a|emotion_dance_t|emotion_dance_v)\.kf$'
```

Add a new empty output directory as the third argument to extract. Each output
includes an `extraction-report.json` with relative names, lengths and SHA256.
The Character archive contains the previously missing player animations.
DDS recovery searched Textures, Effect, Map, Item, Npc and Character in both
installed clients. See `Native/STATUS.md` for the four unresolved texture names.

The tracked `acceptance-plan.json` exports twelve body, equipment, NPC and map
fixtures. Its `itemModel` entries consume copied source XML under NativeSources.
In the frontend, run the actual shared-skeleton fixture tests against that output:

```powershell
$env:NATIVE_ACCEPTANCE_DIR='static/gltf/native-acceptance-03'
pnpm exec vitest run tests/nativeAssets.test.ts tests/sharedSkeleton.test.ts tests/nativeOutfitAssets.test.ts
```

Those tests skip the external fixtures when the environment variable is absent.
They compare deformation numerically; they do not establish material appearance.
Use `/outfits` and the normal item/NPC viewer for the remaining visual checks.

### Baseline commands and historical evidence

The C# runner in `NifToGltf.Tests` validates geometry, skinning, interpolation,
attachment and merged animations. Khronos validation additionally checked all
2,359 successful static exports: zero errors, three missing-tangent warning
files. The other 471 inputs failed conversion. Full results are recorded in
[batch-results.json](batch-results.json). A first failure can hide later
unsupported requirements; an export passing validation is not a visual audit.

Install the optional QA validator in an ignored directory, then run:

```powershell
pnpm --dir NifToGltf/obj/research add gltf-validator
node NifToGltf/Diagnostics/validate_gltf.cjs Maple2Storage/Resources/NativeBatch-02 NifToGltf/obj/research/batch-validation-final.json
py -B NifToGltf/Diagnostics/summarize_batch.py Maple2Storage/Resources/NativeBatch-02/batch-report.json NifToGltf/obj/research/batch-validation-final.json --output NifToGltf/Diagnostics/batch-results.json
py -B NifToGltf/Diagnostics/compare_animations.py Maple2Storage/Resources/NativeProof/rabbit-kfm.gltf Maple2Storage/Resources/GLTF/21000174_m_rabbitdollcymbalsgrey --output NifToGltf/Diagnostics/rabbit-animation-comparison.json
```

The comparison requires the existing local Noesis reference exports. It pairs
tracks by exact node name and evaluates native interpolation at Noesis key
times. It records missing tracks and maximum deviations per clip. Twelve
rabbit clips agree within 0.000768 degrees; idle differs by 1.243 degrees at
`Point01` because native preserves the declared quadratic Euler curve.
See [rabbit-animation-comparison.json](rabbit-animation-comparison.json).

For attachment inspection, export the same selected clip on a body and its
grafted gear, then combine their self-contained glTF files:

```powershell
py -B NifToGltf/Diagnostics/compose_proof.py Maple2Storage/Resources/NativeProof/f_body-fitting.gltf Maple2Storage/Resources/NativeProof/hat-fitting.gltf --output ../MapleStory2-Handbook/static/gltf/native-proof/body-hat.gltf
```

The composer rebases object indices and merges same-named clips across the two
skeletons. This is a development proof, not the outfit simulator's shared
skeleton implementation. The local Handbook `/dev/nif-converter` route exposes
body, gear and NPC fixtures with a clip selector and playback controls.
# Gelo equipment extension

The current simulator uses `simulator-release-03`. Rebuild its extension from
the immutable release-02 base and our extracted resources, using fresh paths:

```powershell
py -X utf8 NifToGltf/Diagnostics/extend_simulator.py --base ../MapleStory2-Handbook/static/gltf/simulator-release-02 --resources Maple2Storage/Resources --work NifToGltf/obj/gelo-new-build --output ../MapleStory2-Handbook/static/gltf/simulator-release-03-copy --review NifToGltf/Diagnostics/gelo-library-review.json
```

`gelo-library-plan.json` lists the exact native inputs, attachments and body
variants. Required ignored resources are the original `GeloSources/Item` and
`GeloSources/Face` extracts, plus `GeloMissing/Item/1/34/13400263_fireprismstar.nif`
and `GeloMissing/Makeup/item_makeup/10400107_rosycheeks.dds`. The latter two come
from our installed KMS2 Item.m2d and Textures.m2d archives. Original archives
remain read only. Base resources and source XML paths are unchanged.

The builder converts eighteen models and twenty-three textures, generates the
extended customization catalog, verifies both hand variants and decal hashes,
and writes a new inventory. It does not read a game database or include Gelo's
saved character snapshot. `gelo-library-review.json` records tested states and
limitations. Stowed star exports are research assets; reviewed star workflows
use independent drawn hands. The sign's independent wing animation is absent.
# Motion, compatibility and material refinement

`refine_simulator.py` rebuilds release-04 from immutable release-03 plus our
source extracts. It reconstructs all 144 conversion entries, then applies the
explicit `motion-library-review.json`. The resulting 620 files were rebuilt
twice and matched byte for byte. This command never reads the game database or
publishes assets:

```powershell
py -X utf8 NifToGltf/Diagnostics/refine_simulator.py --base ../MapleStory2-Handbook/static/gltf/simulator-release-03 --resources Maple2Storage/Resources --work NifToGltf/obj/fresh-refinement --output ../MapleStory2-Handbook/static/gltf/fresh-release --review NifToGltf/Diagnostics/motion-library-review.json
```

The input resources include `SimulatorMotion/Item/1/18/11850281_*` NIF/KF,
and `SimulatorMotion/Body/{female,male}` with idle_a, fitting_idle_a, walk_a,
run_a, emotion_dance_t, emotion_dance_v, star_attack_idle_a and star_run_a.
These came from our installed Item.m2d and Character.m2d through the read-only
archive extractor. Existing SimulatorSources/GeloSources/GeloMissing textures,
models and XML remain required. The generated plan lives in the chosen work
directory. Use fresh work and output paths; existing outputs are preserved.

The review lists precise item/body states and unresolved appearance limits.
Changing source data requires new visual review and hashes. Successful conversion
does not promote preview items. The saved Gelo profile remains a separate local
development preview. See Native/STATUS.md for direct T3 evidence and limitations.
