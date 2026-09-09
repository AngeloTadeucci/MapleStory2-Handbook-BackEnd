# Native NIF converter

The opt-in `--native` path reads MS2 NIF 30.2.0.3 and 30.1.0.3 directly and writes glTF 2.0.
Older mannequin geometry is checked against the published vertex and triangle
inventories. Unsupported block types still fail explicitly.
The existing Noesis command remains the default because the non-effect
acceptance gate is not complete. `--noesis` explicitly selects that fallback.
No production assets or database records have been replaced. See
[current status and blockers](STATUS.md) before treating an export as verified.
The [model migration run](MODEL-MIGRATION-RUN.md) records the current shared
viewer, canonical packaging, source coverage, and verified recovery mirror.

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
Player bodies require an explicit list. Both player KF sets were located in
the installed KMS2 `Character.m2d` archive. The acceptance plan selects six
clips per body: idle, fitting idle, walk, run and two dances. KF 30.1.0.3
is supported alongside 30.2.0.3. No source root-motion track is stripped.

`--skeleton` copies the full named body hierarchy. Skinned clothing rebinds to
those bones while retaining its source inverse bind matrices. Rigid `CP`
equipment attaches to `Bip01 Head`; other rigid slots require `--attach` with
an exact body bone name. Unverified slot mappings are not inferred.

## Output contract

- One `.gltf` file, embedded binary buffer and PNG images. DDS DXT1/3/5,
  uncompressed RGB channel masks and supported embedded NiPixelData base
  mipmaps are decoded locally, without Noesis or an image dependency.
- Source node names and local skeleton units remain intact, including helper
  bones such as `SATA9NI_Bone01`. A wrapper converts Z-up centimeters to Y-up
  meters. Do not apply the old Noesis orientation or another centimeter scale.
- glTF skins use remapped palettes and four normalized influences. Duplicate
  influences are combined. Skinned mesh instances sit at scene roots; joint
  world transforms drive their deformation.
- Each selected animation is a named entry in `animations`. Meshes and
  materials are emitted once. Animation targets bind to unique exact names.
  Multiple authored sequence roots receive explicit sequence labels. Colliding
  KFM names retain their event IDs. Animation extras preserve source sequence
  names, root block IDs, and KFM event IDs instead of silently choosing a root.
- Diffuse, normal, emissive and alpha properties map to basic glTF materials.
  Client default override colors are baked using the observed ColorOverride
  shader formula. Both diffuse and control resolution are retained. Original
  textures and colors remain in `materials[].extras` for customization.
  `nifBaseColorTexture` and `nifColorControlTexture` identify the original maps;
  `nifOverrideColors` retains the three source RGB values. The outfit viewer
  exposes per-material color controls and reset using this data.
  This does not implement the client's full lighting or face animation.
- Per-texture UV matrices are baked into additional UV sets. Texture sampler
  wrap/filter modes are retained. Missing tangents are generated from triangle
  derivatives. Triangle strips are triangulated with alternating winding.
- Relative position morphs retain the absolute base, offset targets and default
  weights. Absolute/normalized morphs and normal-recomputing morphs still fail.
- Source hidden meshes and descendants of hidden nodes are excluded from
  rendering and reported in `hiddenMeshes`.
- Batch mode preserves relative directories and separates converted, excluded,
  missing and failed entries in `batch-report.json`. Without `--manifest` it
  scans static NIFs. A version 1 plan provides explicit clips and skeletons.
  `native-manifest.json` contains portable URLs, clips and equipment metadata.
- Particle systems are omitted separately from ordinary geometry. Physics
  resources are omitted from the visual scene and reported. Reflection effects
  and billboards are not silently discarded. Effect folder names are not a
  reliable exclusion rule: instruments, lift-up props and capes occur there.

## Selected batches and Handbook integration

The [acceptance plan](../Diagnostics/acceptance-plan.json) is relative to
`Maple2Storage/Resources`. Extracted inputs remain in dedicated directories.
Use a new empty output directory for each run:

```powershell
dotnet run --project NifToGltf -- --native --batch --input Maple2Storage/Resources --output ../MapleStory2-Handbook/static/gltf/native-acceptance --manifest NifToGltf/Diagnostics/acceptance-plan.json --textures 'Maple2Storage/Resources/Models/Textures;Maple2Storage/Resources/NativeSources;Maple2Storage/Resources/NativeRecovery-05'
```

Each plan model can specify `input`, `output`, `id`, `kfm` or `animations`,
`clips`, `skeleton`, `attach`, `slot`, `bodyVariant`, and an evidenced
`excludeEffect` reason. `itemModel` plus `itemId` reads the client's XML
attachment for that exact source and gender. It preserves `selfnode`,
`targetnode`, replacement and cutting metadata. Do not combine this with
an explicit `attach`. Attached private skeletons still require more work.

The Handbook reads `native-manifest.json` from the configured model base URL,
or `/gltf/` in development. Manifest URIs are relative to that JSON file.
If publishing a nested output under that base, prefix its URIs accordingly.
The item/NPC viewers prefer a uniquely matched native asset and use its merged
clips without the Noesis rotation. They retain legacy lookup when no match exists.

`/outfits` uses Three.js, exact source names and one body mixer. Equipment skins
share body bone objects and retain inverse bind matrices. Attachment helper
nodes unused by the naked body's skins are promoted to joints without renaming.
The consumer rejects mismatched rest poses and missing bones. XML replacement
and cutting names hide the corresponding body nodes. CL replacement retains
sibling CL_* garment parts while hiding the naked
body's CL_* parts. The female acceptance top is the textured 11400158 hoodie;
the old 11400182 untextured test mesh is no longer used as clothing acceptance.
The male acceptance item is 11400350, using the current-client sleeveless hoodie.
Its exposed-arm seam matches the body. The staged 11400040 shirt has an
incompatible wrist opening and is no longer used as outfit acceptance.
The viewer supports selection, play/pause, source-mask color controls, camera
controls and PNG capture. Full simulator customization and
client materials remain incomplete.

The outfit body owns the skin palette. Equipped MS2CharacterSkinMaterial parts
inherit it, and the Skin controls edit/reset all body and equipment skin together.
Other material dyes remain independent. Standalone exports retain authored colors.

Nonlinear transform curves use a 60 Hz sampling grid plus source key times and
adaptive subdivision. Interpolation checks use 0.05 degrees for rotation,
0.001 source units for translation and 0.00001 for scale. These are sampled
error checks, not a mathematical bound over every continuous time. Compact
B-splines, linear/step keys and quadratic scalar/vector/Euler keys are supported.
TCB curves and quadratic quaternion curves are rejected when interpolation is
required. No animation error silently removes a selected clip.

## Historical verification before this continuation

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

See [STATUS.md](STATUS.md) for current evidence, remaining failures and the
acceptance gaps. Reflection materials, billboards, face/material animation,
TCB interpolation, ambiguous sequences, private equipment bones and visual
verification must be resolved before switching the default.

## Verification commands

```powershell
dotnet build NifToGltf/NifToGltf.csproj
dotnet run --project NifToGltf.Tests -- Maple2Storage/Resources/Models/Character/female/f_body.nif
py -B -m unittest discover -s NifToGltf/Diagnostics -p 'test_*.py' -v
```

The full C# runner currently passes 59 tests with the available source fixtures,
including body/gear geometry, thirteen rabbit clips, palette handling, TCB
interpolation, older mannequin files, and sequence/event identities. Without
the body argument it runs only the synthetic unit cases.
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
