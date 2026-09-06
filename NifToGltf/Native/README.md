# Native NIF converter

The opt-in `--native` path reads MS2 NIF 30.2.0.3 directly and writes glTF 2.0.
The existing Noesis command remains the default. No production assets or
database records have been replaced.

## Run

Run these commands from the backend repository root. Output directories below
are ignored game resources. Existing single outputs require `--overwrite`.
Batch destinations must be empty. A failed conversion exits 1.

```powershell
dotnet run --project NifToGltf -- --native --help

dotnet run --project NifToGltf -- --native --input Maple2Storage/Resources/Models/Character/female/f_body.nif --output Maple2Storage/Resources/NativeProof/f_body.gltf

dotnet run --project NifToGltf -- --native --input Maple2Storage/Resources/Models/Item/1/13/11300212_c_duckyballcap01_c.nif --skeleton Maple2Storage/Resources/Models/Character/female/f_body.nif --textures Maple2Storage/Resources/Models/Textures --output Maple2Storage/Resources/NativeProof/hat.gltf

dotnet run --project NifToGltf -- --native --kfm Maple2Storage/Resources/Models/Npc/21/21000174/21000174_m_rabbitdollcymbalsgrey.kfm --clips all --textures Maple2Storage/Resources/Models/Textures --output Maple2Storage/Resources/NativeProof/rabbit.gltf

dotnet run --project NifToGltf -- --native --input Maple2Storage/Resources/Models/Character/female/f_body.nif --animations Maple2Storage/Resources/Models/Npc/11/11000148 --clips fitting_idle_a --output Maple2Storage/Resources/NativeProof/body-fitting.gltf

dotnet run --project NifToGltf -- --native --batch --input Maple2Storage/Resources/Models --output Maple2Storage/Resources/NativeBatch --textures Maple2Storage/Resources/Models/Textures
```

`--clips` selects KFM sequence names or KF basenames when using `--animations`.
Player bodies require an explicit list. The available fitting idle proves
binding but does not replace the full player animation export. The complete
1,995-clip set referenced by plan 9 has not been located in the inspected tree.

`--skeleton` copies the full named body hierarchy. Skinned clothing rebinds to
those bones while retaining its source inverse bind matrices. Rigid `CP`
equipment attaches to `Bip01 Head`; other rigid slots require `--attach` with
an exact body bone name. Unverified slot mappings are not inferred.

## Output contract

- One `.gltf` file, embedded binary buffer and PNG images. DDS DXT1, DXT3 and
  DXT5 base mipmaps are decoded locally, without Noesis or an image dependency.
- Source node names and local skeleton units remain intact, including helper
  bones such as `SATA9NI_Bone01`. A wrapper converts Z-up centimeters to Y-up
  meters. Do not apply the old Noesis orientation or another centimeter scale.
- glTF skins use remapped palettes and four normalized influences. Duplicate
  influences are combined. Skinned mesh instances sit at scene roots; joint
  world transforms drive their deformation.
- Each selected animation is a named entry in `animations`. Meshes and
  materials are emitted once. Animation targets bind to unique exact names.
- Diffuse, normal, emissive and alpha properties map to basic glTF materials.
  Additional shader textures, including control masks, remain embedded and
  referenced by `materials[].extras.nifTextures`. These are texture indices,
  not an implemented dye shader. Image names retain source filenames.
- Source hidden meshes and descendants of hidden nodes are excluded from
  rendering and reported in `hiddenMeshes`.
- Batch mode preserves relative directories and records every rejection in
  `batch-report.json`. It currently exports static models. It does not infer
  per-equipment skeletons, attachment mappings or per-NPC clip selections.

Nonlinear transform curves use a 60 Hz sampling grid plus source key times and
adaptive subdivision. Interpolation checks use 0.05 degrees for rotation,
0.001 source units for translation and 0.00001 for scale. These are sampled
error checks, not a mathematical bound over every continuous time. Compact
B-splines, linear/step keys and quadratic scalar/vector/Euler keys are supported.
TCB curves and quadratic quaternion curves are rejected when interpolation is
required. No animation error silently removes a selected clip.

## Verified on 2026-09-06

The female body exports 2,034 vertices and 2,762 triangles across ten meshes.
Source bind matrices cancel bone world transforms within 0.0001 source units
in the regression test. All ten decoded body textures matched Pillow pixels.
Female and male bodies, a duck hat, clothing, Balrog and the rabbit NPC were
loaded in the Handbook development viewer. The hat stays on the animated head.
The rabbit's shape and framing were compared visually with the existing Noesis
export. Balrog playback and the thirteen-clip rabbit selector were exercised.
All eight development preview files, including the Noesis reference, pass
glTF validation without errors or warnings. Animated sample times are deduplicated
at float32 precision before serialization to preserve strictly increasing keys.

The full static batch exported **2,359 of 2,830** inputs and rejected **471**.
All 2,359 exports passed Khronos glTF validation with zero errors. Three files
have missing-tangent warnings. See [batch-results.json](../Diagnostics/batch-results.json)
for every rejected input, its first failing requirement, and warning paths.
Schema validation does not establish visual fidelity for all 2,359 files.

[Rabbit comparison](../Diagnostics/rabbit-animation-comparison.json) samples
native animation at the existing Noesis key times. Twelve of thirteen clips
agree within 0.000768 degrees and 0.000239 source units. Idle differs by up to
1.243 degrees on `Point01`: the source declares quadratic Euler keys, while
SLERP between the source key rotations reproduces the Noesis track within
0.00003 degrees. Native output preserves the declared quadratic curve. This
reference comparison is not proof of all client animation behavior.

## Remaining work before replacing Noesis

1. Implement and verify missing scene classes, embedded pixels, UV transforms,
   mesh modifiers, primitive types and DDS formats listed in the batch report.
   Re-export the 127 inputs whose requested external textures were not found
   beside the model or under the supplied texture root once assets are available.
   Counts reflect the first failure per input; later requirements may also fail.
2. Complete game material behavior. The body preview still shows overlapping
   facial atlas details and uncustomized white skin. Generic glTF material output
   does not implement face selection, skin color, dye, specular maps, texture
   controllers or the client shader. Geometry and attachment proofs do not
   establish character appearance parity. Inspect client material configuration
   and a reference character render before changing the face UVs or shader.
3. Validate TCB quaternion interpolation before enabling it. Balrog's 65-entry
   manifest rejects `Attack_01_G`, `Attack_01_H`, `Attack_02_G`, `Attack_02_H`.
   `Attack_Idle_A.kf` has two roots with the same sequence name, 102 evaluators
   each, and different durations. KFM name lookup cannot select uniquely.
   Determine the client's sequence selection rule before choosing one.
4. Obtain the full player KF set, select curated clips, verify root motion and
   equipment across the intended body variants, and supply explicit attachment
   metadata for rigid slots other than CP.
5. Add animated batch manifests and the production frontend integration from
   plan 8. The development proof route is not the outfit simulator.

## Verification commands

```powershell
dotnet build NifToGltf/NifToGltf.csproj
dotnet run --project NifToGltf.Tests -- Maple2Storage/Resources/Models/Character/female/f_body.nif
py -B -m unittest discover -s NifToGltf/Diagnostics -p 'test_*.py' -v
```

The C# runner has 19 tests, including real body/gear fixtures, all thirteen
rabbit clips, palette handling, numerical interpolation and ambiguous sequence
rejection. Without the body argument it runs only the synthetic unit cases.
See [diagnostic instructions](../Diagnostics/README.md) for the full validator
and reference comparison commands. The converter has no new runtime packages.

The development route in the sibling Handbook is `/dev/nif-converter` and
returns 404 outside development. It reads ignored `static/gltf/native-proof/`
fixtures. `compose_proof.py` can combine body and gear exports for inspection,
including clips with the same name. It keeps separate skeletons and is not a
production outfit composition implementation.

Format references: [nifxml](https://github.com/niftools/nifxml/blob/develop/nif.xml),
[NifSkope interpolation](https://github.com/niftools/nifskope/blob/develop/src/gl/glcontroller.cpp),
[glTF 2.0](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html).
