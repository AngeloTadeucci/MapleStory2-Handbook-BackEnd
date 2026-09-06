# Plan 9: format survey and conversion verification

The scan on 2026-09-06 disproved the original four-format assumption. The user
authorized continuing with the corrected nine-format inventory. Native body
conversion, skeleton grafting, merged animation and a static batch are now
implemented. Noesis remains the default. See the [native converter](../Native/README.md)
for commands, verification results and the remaining fidelity requirements.

## Reproduce

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
