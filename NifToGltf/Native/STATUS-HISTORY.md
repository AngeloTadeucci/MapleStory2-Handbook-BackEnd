# Historical simulator checkpoints

Archived on 2026-09-07. Server addresses, permissions and next actions below
are historical. Read [STATUS.md](STATUS.md) for the current handoff.

# Simulator implementation status, 2026-09-07

## Wardrobe candidate 14, 2026-09-07

Implemented on Oracle in both `feat/clothing-simulator` checkouts. Initial fetch
found both branches equal to upstream. Pre-existing Oracle preparation notes and
test fixes remain. No commit, push, merge, deploy or database write was performed.
The original review process on 127.0.0.1:4000, PID 2350807, remains untouched.
All commands require `source NifToGltf/obj/oracle-prep/env.sh` first.

### Coverage and discovery

The matching `$KMS2_DATA` itemdata contains 36,022 IDs. The authoritative inventory
accounts for 19,268 wearable IDs and 33,170 eligible body pairs: male 16,575 and
female 16,595. Exclusions are explicit: 15,313 nonwearable IDs, 1,397 without an
enabled KR Live environment, and 44 badges. Actual itemmodel slots are inventoried
without the old slot allowlist, including FH, EY, PD, RI, BE and ER. The 746
Item/Empty.nif pairs are explicitly nonvisual, with the no-mesh source inspected.
Every scoped pair is searchable, even if unavailable. Model sharing does not
collapse item IDs or body eligibility. Database SELECT reconciliation found 118
scoped IDs absent, 535 blank DB names and zero gender mismatches. Source labels
include 1,831 unusable-name pairs and 17,831 declared icons absent from Image.m2d.
The UI uses a numeric fallback name and placeholder icon, and exposes reasons.

`static/gltf/simulator-release-14` is the concrete expanded library. It contains
9,329 model assets and 7,078 usable bundle families out of 8,978 scoped families.
Catalog labels: **80 inherited reviewed, 27,788 preview, 5,302 unavailable**.
Conversion states: baseline-reused 675, converted 28,096, failed 3,231,
not-converted 962, customization-only 206. These are separate from visual acceptance.
All 10,464 initial model/form/placement jobs were attempted. Six baseline-only
records are explicitly reconciled: two faces had the wrong eligible body and
four cape pairs used model IDs absent from itemdata. Release-05 is unchanged.

### Implementation and source evidence

`wardrobe_inventory.py` reconciles complete selected environments, presets,
attachments, customization, cutting, exact archive paths, missing sources and
nonvisual records. It retains source trees and hashes in obj/wardrobe.
`expand_wardrobe.py` groups reusable sources and runs locked, restartable batches
of 64, with two workers and an 8 GiB floor plus reserves. `retry_wardrobe.py`
records generation precedence, exact binaries and source evidence. Failed newer
checks invalidate old successes; a stale static export cannot mask a controller.
Assembly records Linux/Windows provenance without rewriting baseline review hashes.
Portable PDB document checksums in NifToGltf.Provenance identify actual compiler
sources independently of working-tree snapshots made while older binaries ran.

Shared repairs cover GL_Wrist/GL_Skin siblings; entire Scene Root attachments;
unused duplicate body names; HR0/HR1 and HR:0 forms; genuinely empty KFM lists;
private replacement-hair bind space; embedded looping transform controllers;
validated private multi-clip playback; source palette cycles; face sequences;
masked makeup placements and scale ranges. Private hair 10200183 originally floated
beside the head under a hat. The corrected parent preserves source joint world
transforms and bind matrices under the animated head. All 32 affected replacement
assets were rebuilt, with no geometry rescaling or animation-key modification.
Ponytails with unimplemented custom/jointangle transforms remain unavailable.
Single-axis dummy rotations now retain authored degrees and translations without
assuming a multi-axis order: 510 jobs rechecked, 476 converted and 34 failed,
recovering 1,500 usable pairs. Multi-axis rotations remain visibly blocked.

The archive texture audit compared all 34,484 prepared DDS files with matching
Textures.m2d: 34,405 matched, 18 differed and 61 were absent. Thirteen exported
models used differing textures; all 13 were rebuilt from exact matching bytes.
None used the 61 absent extras. The prepared texture directory is untouched.
`matching-textures/` links verified files and separately extracted corrections.
Missing channels recovered from matching Effect.m2d restored 156 other models.
Crusader glove zero normals were inspected at indexed source vertices, including
nonzero-area triangles. No required geometry was removed or normals invented.
Exact failures, animation targets and per-pair limitations ship in candidate JSON.

### Verification and remaining acceptance

C# 45 source/logic tests and Python 17 tests pass. Final frontend check reports
0 errors and 0 warnings; 115 frontend logic/source tests pass. All 9,327 equipment
assets pass the asset-backed suite in 37 bounded batches. All 9,329 glTF assets
validate with zero errors or warnings. The 42 complete-outfit cases in the final
capture groups cover 77 distinct item/body pairs, and their contact sheets were
inspected. This includes all 13 assets rebuilt for texture-revision corrections.
The fixed-scale hair control defect found in the UI workflow is corrected:
fixed source scales do not offer a morph selector; adjustable lengths have an
explicit reset to the recorded native default, even when it is outside the
selectable list. Reset capture precedes restoration of saved hat-fitting values.
Three focused tests cover fixed scales, adjustable values above one and reset.
The final production build in obj/wardrobe/build-14c passes. Its compiled public
UI workflow passes on both bodies with no page errors, including hair-length reset.
The detailed development workflow passes all 17 states, including private Gelo.
The final compiled hair test explicitly equips adjustable hair 10200006/female
and 10200001/male without hats. It requires a visible control, changes the length
and verifies reset to the native default. An earlier public run had zero controls
under hats, so its skipped loop was not reset evidence. That test gap is corrected;
ui-built-14-adjustable/results.json records positive control counts for both bodies.
All 12,146 packaged release files match their inventory hashes. Only candidate 14
is packaged; actual private Gelo paths and release-05 paths return HTTP 404.
The compiled candidate runs at http://127.0.0.1:4003/outfits, PID 2531003.
The separate development preview remains on port 4002, PID 2512871.
The machine-readable [release report](../Diagnostics/wardrobe-release-report.json)
records denominators, conversion counts, exact inspected pairs and evidence hashes.
There are 7,515 families with complete conversion or customization records;
7,078 are usable after compatibility checks. Neither count means visual acceptance.

No new entry has been promoted to verified. Contact sheets and exact-state
records are in obj/wardrobe/family-review-14; prior failures and repairs remain
in family-review-11 and private-hair-review-12. Each case records actual equipped
bundles, item IDs, bodies, hair forms, source files, pose, time and captures.
Release-05 and Gelo-07 preservation check: 1,273 files hashed, zero differences.
Private Gelo remains development-only and excluded from the compiled asset tree.
T3 preview_open/status report no automation host for this environment. Oracle
Chromium/Playwright is used; T3 interaction has not been possible.

Hunya's public clothing selector was exercised with 11400049/female. It equipped
the Crusader top with palette 10/index 9 colors 63,59,51 and 20,18,15. Captures and
read-only request records are in reference-crusader-equipped.json/png, with local evidence in
crusader-palette-review-14. Its update
notice says item equipment is broken after the April 2026 host migration, but the
specific 11400049 selection did render. Earlier loading-only observations were
incomplete. No save/publish action was used; POST requests were blocked. The local
default colors differ from its icon and this reference selection. The explicit palette 10/index 9 comparison matches the white-cross/dark-coat
color arrangement in the local capture on both bodies; the source omits defaultColorIndex, so its fallback
cannot be inferred from a successful conversion. The catalog exposes that limit.

Remaining blockers are recorded per pair, not omitted scope: multi-axis dummy
rotations, source animations targeting unsupported attachment nodes, unresolved
hat fitting and ponytail controls, certain lights/material controllers, source
geometry defects, missing archives/models and private-artwork UGC templates.
Ordinary supported decoration motion remains animated. Cloth/physics and broader
particle effects are not implemented. Existing accepted shader and hair-twinkle
effects are preserved. Baseline paired-star stow overlap remains a visible limit.
This is an expanded review candidate, not complete visual coverage.

Reproducible commands and checkpoints:

```bash
python3 -B NifToGltf/Diagnostics/wardrobe_inventory.py --xml NifToGltf/obj/wardrobe/xml --index NifToGltf/obj/wardrobe/archive-index.json --output NifToGltf/obj/wardrobe
python3 -B NifToGltf/Diagnostics/expand_wardrobe.py prepare
python3 -B NifToGltf/Diagnostics/expand_wardrobe.py run --batches 200 --batch-size 64 --workers 2
python3 -B NifToGltf/Diagnostics/wardrobe_customization.py --output NifToGltf/obj/wardrobe/customization-02
python3 NifToGltf/Diagnostics/expand_wardrobe.py assemble --output ../MapleStory2-Handbook/static/gltf/simulator-release-14 --patches NifToGltf/obj/wardrobe/patches --customization NifToGltf/obj/wardrobe/customization-02
node NifToGltf/Diagnostics/verify_wardrobe.mjs ../MapleStory2-Handbook/static/gltf/simulator-release-14 NifToGltf/obj/wardrobe/asset-tests-14
node NifToGltf/Diagnostics/build_wardrobe.mjs NifToGltf/obj/wardrobe/build-14c
node NifToGltf/Diagnostics/browser_wardrobe.mjs NifToGltf/obj/wardrobe/family-cases-14.json NifToGltf/obj/wardrobe/family-review-14
WARDROBE_EVIDENCE_DIR=NifToGltf/obj/wardrobe/ui-workflows-14-complete node NifToGltf/Diagnostics/outfit_workflows.mjs
WARDROBE_EVIDENCE_DIR=NifToGltf/obj/wardrobe/ui-built-14-adjustable node NifToGltf/Diagnostics/built_outfit_workflows.mjs
python3 NifToGltf/Diagnostics/report_wardrobe.py --library ../MapleStory2-Handbook/static/gltf/simulator-release-14 --work NifToGltf/obj/wardrobe --build NifToGltf/obj/wardrobe/build-14c --output NifToGltf/Diagnostics/wardrobe-release-report.json
```

Use fresh output paths for assembly/build/customization. Existing successful
batches are checkpoints, not disposable retries. Removed session-owned intermediate
release trees retain root metadata/hashes under obj/wardrobe/intermediate-metadata.
Source batches, versioned converters, screenshots and baselines remain preserved.

## Oracle workspace preparation, 2026-09-07

The user requested preparation of oracle-machine for overnight wardrobe work.
Both repositories now exist under `/home/ubuntu/repos`, on
`feat/clothing-simulator` at backend 53a7196 and frontend d749a28, plus the
uncommitted preparation notes and two test corrections described below.
Full wardrobe implementation has not started. No commits or pushes were made
in this preparation, and neither master branch was changed.

Before any commands on Oracle, source
`~/repos/MapleStory2-Handbook-BackEnd/NifToGltf/obj/oracle-prep/env.sh`.
It selects the workspace-local ARM64 .NET SDK 8.0.424, existing pnpm launcher,
backend/frontend paths, matching KMS2_DATA and cached ARM64 Chromium. Node 24.18.0,
pnpm 10.4.1, Python 3.10.12 and tmux 3.2a are available. Codex 0.144.1 reports an
authenticated login; a model request and remaining account quota were not tested.
No system packages, global tool versions, service configuration or existing
deployment processes were changed.

SHA256 verification covers 716 archive/shader files, 56,864 extracted simulator
source files, 2,512 release/private-preview files, 118 regression fixture files
and 14,356 existing item icons. The matching Image.m2d and Image.m2h were also
transferred and hashed. Client archives are under
`Maple2Storage/Resources/KMS2-Data`; retain their original Resource/Model layout.
Release-03/04/05, native-acceptance-06, Gelo-07 and shader evidence are available.
Gelo remains private and ignored; the production-build review server intentionally
does not enable the development-only character-preview route. Prior Oracle client
archives are different revisions and must not silently replace this KMS2 source.
Verified duplicate transfer archives were removed from obj; about 27 GiB remained.
Use bounded batches and check disk before each batch; leave at least 8 GiB free.

An existing Docker Handbook deployment and local maple2_codex database were
found. A SELECT counted 39,553 items. The frontend's private mode 0600 .env uses
that existing connection. Its account has ALL PRIVILEGES on maple2_codex, so
read-only use is an instruction, not an enforced database permission boundary.
Do not run GameParser, migrations, tracking POST routes or any database writes.
The deployment and database were not modified. Preview icons use local assets
because the deployed CDN configuration rejected the loopback preview origin.

Oracle verification: 35 C# source tests, 14 Python tests and all 249 frontend
tests passed with asset suites enabled. Svelte check had zero errors/warnings;
the production build completed and all 635 packaged release files match their
inventory. Two existing test problems were corrected on both machines:
NifToGltf.Tests uses the actual lowercase attack_idle_a.kf fixture filename;
frontend unescapeHtml tests use describe rather than nesting tests inside test.
Existing nonfatal build warnings remain. Archive extraction read itemdata/102.xml
from the copied Xml.m2d. The motion plan converted 5 assets with no failures.

Cross-platform export comparison: all 45 decoded PNG images have identical
pixels and integer accessors match. The female and male body exports differ
in 12 and 8 float values respectively, maximum absolute delta 5.960464477539063e-8.
The three equipment exports have identical numeric accessors. JSON differences
are confined to embedded image/buffer URIs. Compressed PNG bytes differ, so Linux
exports are not byte-identical to Windows. Do not bypass review hashes by
silently rehashing Windows acceptance records against newly exported assets.
See obj/oracle-prep/motion-comparison.json and the retained smoke outputs.

The new review process binds only 127.0.0.1:4000 on Oracle; its PID and log are in
obj/oracle-prep/preview.pid and preview.log. Windows tunnel 127.0.0.1:4001 reaches
it without disturbing the existing Windows 4000 preview. This is a built review
server, not a dev server; it does not hot reload. T3 opened the Oracle route
and equipped the complete female outfit through its catalog without alerts.
Oracle Playwright/SwiftShader
loaded complete nine-item outfits on both bodies: female 10300003/10200124,
male 10300001/10200121, shared 11400002, 11500004, 11600004, 11700004, 11200001,
11800001, 11300001. Each used fitting_idle_a, Happy, Henesys, front view and
paused playback. Pause time was not fixed. Screenshots were inspected; no new
appearance acceptance was granted to preview items. Both flows had no application
errors. The harness intentionally blocked 4 non-GET requests. A pre-existing
nonessential /icon0.png request still returns 404 and is explicitly recorded.
Reports and captures: obj/oracle-prep/browser-smoke.json and *-oracle-outfit.png.
These checks establish workspace readiness, not complete wardrobe coverage or
pixel parity with the client. No overnight implementation agent was launched.

## Wardrobe expansion plan, 2026-09-07

The user prioritized complete clothing/decorations coverage before save/share
and requested a plan. SIMULATOR-PLAN.md now defines inventory reconciliation,
full catalog discovery, conversion by family, equipment fixes, visual review
and expanded-release gates. No expansion is implemented in this planning turn.
The current coverage.json was rechecked: 153 models, 127 item/body entries,
82 reviewed, 35 previews, 10 unavailable. The complete client total is unknown.
Inspected constraints: simulator_library.py uses per-slot sampling, signature
deduplication, a slot allowlist and itemmodel-based enumeration; the API requires
nonempty DB names and primarily uses DB slot/gender filters. These must not be
mistaken for a complete wardrobe inventory. Existing release assets are unchanged.

## Source review checkpoint, 2026-09-07

The user authorized new `feat/clothing-simulator` branches from local master in
both repositories, including the four earlier simulator commits and the effects
and shader changes below. Source, tests and evidence records belong to this
checkpoint. Generated client assets, captures and the private Gelo snapshot stay
local and ignored. Deployment is not authorized. Verification below covers the
implementation; this checkpoint changes branch/documentation state only.

## Character shading correction, 2026-09-06

The user identified the surface shader as the issue and authorized fixing it.
The frontend now uses source material ambient independently of material diffuse,
the authored Fresnel boost/exponent, and the hair Shader1 direction texture for
the client's combined anisotropic and normal-based highlights. Gloss and hair
direction maps remain linear data; dye and face textures are unchanged. The
directional specular coefficient now uses the same irradiance conversion as
diffuse. Three's physical indirect specular path is disabled for these materials.
There was no environment map in Gelo's scene, so that indirect path was not the
main cause of the observed dark shading. Ambient lighting and its material
calculation account for the large visual correction.

Read-only extraction of our Exported.m2d confirms character_spring2019 inherits
MS2AmbientLight_Outside_ and MS2DirectionalLight_Outside_. Both presets have white
light and Dimmer=0.8; directional AmbientColor is black. The studio replaces its
dark hemisphere with uniform ambient and uses these coefficients, converted by
PI to Three irradiance units. Material ambient is no longer multiplied by the
diffuse factor a second time. The existing key direction (3,5,4) is retained;
the new rim direction (0,-1,0) is a documented studio choice, not a recovered
client global. No geometry, body scale, saved dyes or hair effects were changed.

Extract manifests in obj/shader-comparison/sources and presets record SHA256.
The spring xblock is 60633ae1ff503cd6ac432231263c1991a69b9511e4a638a9932fb5dc6ca07fa5;
ambient preset is 3d5baf6e9037fc923e7ab2c7c928a6fae5110d849a20cd6b81b2abf6342ebb77;
directional preset is 3ec8fa1ab1aad984218d849b5650a94783af01fbd4276fc2d184eb1fce5d7a73.

Evidence is under ignored obj/shader-comparison: source extracts and five initial
captures in evidence/. The before/after Gelo captures use fitting_idle_a at
0.65 seconds, studio background, twelve saved instances and default expression.
Final front/side/back checks retain all items. Happy expression with Henesys and
star_attack_idle_a at 0.65 seconds exports a decoded 823x520 PNG with the effect
enabled, particle counts 3/1. Male 10200121, face10300001, top11400002,
pants11500004, gloves11600004, shoes11700004, earrings11200001 and cape11800001
were checked from three angles. Hat11300001 selects C hair; explicit blue dye
persists. Six remove/equip cycles with the hat kept 22 geometries/63 textures.
No shader compilation errors were reported in either final rendered outfit.

214 focused tests passed; the final material/effect tests were rerun, 15 passed.
Svelte check reports zero errors/warnings. The frontend dev converter helper
characterShaderAcceptance.ts runs nine numerical samples of the production GLSL
on the GPU and two full-material render tests. Both pipelines match expected
linear pixels 0.72 with specular disabled and 1.52 enabled, proving ambient is
independent of diffuse and that diffuse/specular use consistent light units.
All eleven GPU checks pass. The 30 hair materials in release-05 have Shader1
direction maps. The 635 asset files are unchanged by this frontend correction.
The final production build passes. All 635 packaged asset files match their
inventory hashes, and only simulator-release-05 is packaged under gltf. Existing
unused-import, optional client-hook export and chunk-size warnings remain.

Limits: exact running-client parity remains unverified. The shader's skin
subsurface term and the runtime character/rim directions require client global
values; this change does not invent those as recovered data. The source spring
scene coefficients are used for the studio across backgrounds, not per-map
lighting. Hunya's inspected outfit/dyes differ from Gelo, and Odyssey was reviewed
as code only. Neither was treated as an exact matched visual reference. No
commits, deployment, database writes or existing-process stops occurred.

## Hair effects pilot, 2026-09-06

The user authorized cosmetic effects and explicitly excluded badges. The first
supported family is Item/Hair/Eff_Hair_Twinkle_a: two source mesh emitters and
the animated glow layer. Itemdata selects this exact effect for idle and battle
idle on 10200121/10200122 male and 10200123/10200124 female. The effect XML
attaches to Bip01 Head with applyNodeTransform=true and an identity offset.
Gelo's current local preview is `/outfits?preview=gelo-07`, with all twelve saved
instances and original dyes. The Hair effects checkbox hides/freezes this family.
There is no badge loader, selector or badge particle simulation.

`Diagnostics/export_hair_effect.py` reads the inspected NIF revision, fails on
unknown bytes, and exports source emission meshes/normals, rates 5/3, capacities
5/3, speeds 60/0, size/lifetime variations, grow/shrink times, material colors,
glow texture transforms and quadratic alpha/scale keys. Source textures are
hitlight_8-2, gradient_light_02, one_002 and alpha_0352. The source head's 0.01
unit scale applies to positions, velocities and sizes. The first render omitted
that scale on speed/size and was rejected. Corrected captures supersede it.

`Diagnostics/build_effects_release.py` extends immutable release-04 using the
nine converted A/C/D forms in effects-hair-plan.json. Default release-05 contains
153 models and 127 entries: 82 previously reviewed geometry entries, 35 previews,
10 unavailable. The three added hairs and particle appearance remain previews.
The inherited appearance-review.json records base geometry only; the effect
extension report identifies the separate scope. All 635 inventory files matched
a repeat build. New glTF validation: nine models, zero errors and warnings.
The four effect textures and nine new models use our client assets only.

Verification: 211 focused frontend tests pass, including nine effect tests and
152 actual-library binding tests. The effect tests also pass without local asset
fixtures. Two Python effect-export tests and the three simulator packaging tests
pass. Svelte check reports zero errors/warnings. Runtime verification in T3:

The final `pnpm build` passes. All 635 files in build/client/gltf/simulator-release-05
match their inventory hashes. Only the selected release is packaged under gltf;
private character snapshots and research candidates remain excluded. Existing
nonfatal dependency, optional-hook-export and chunk-size warnings remain.
This verifies the package, not a deployed production runtime.

- Female Gelo 10200124, fitting_idle_a at 0.65 seconds, twelve saved instances.
  Source sparkle counts 3/1. Checkbox-off hides the effect and freezes its clock.
  Thirteen remove/equip cycles left no attached effect after removal; the final
  eight cycles stayed at 28 geometries/93 textures. Earlier counts rose from
  90 to 93 textures before settling; no further growth was observed.
  An intentional effect 404 preserved the previous effect object and all items.
- Playback star_attack_idle_a: effect time 0.65 to 4.6667, counts 3/1 to 2/3,
  body time 4.6667. Recording browser-recording-mtql2htn.webm under the local
  T3 browser-artifacts directory. No particle pool growth beyond source capacity.
- Male 10200121 with face10300001, top11400002, pants11500004, gloves11600004,
  shoes11700004, earrings11200001, cape11800001. Hat11300001 selects C hair;
  removing it restores A. Hair dyed RGB0.3,0.5,0.7 remains blue with the hat.
  Replacing it with 10200122 was checked in star_run_a at 0.65 seconds.
- Female 10200123 with face10300003, robe12200002, gloves11600004,
  shoes11700004, earrings11200001 and cape11800001: front/side/back at 0.65
  seconds and hat11300001 C form. Changing bodies clears the previous effect.
- The final source UV/sampler implementation was rendered again with Gelo.
  Henesys plus Happy expression and enabled effects produced a PNG that decoded
  to 823x520, 690345 encoded bytes, with twelve equipment instances. This closes
  the previously unverified Henesys/Happy export combination for this candidate.

Limits: this is a preview of one cosmetic effect family, not a general particle
engine. The seeded random sequence, face sampling, continuous drag integration,
studio material lighting and billboard orientation have not been matched to the
running client. Seeking rebuilds particles at the selected pose, not an invented
prior body trajectory. Other effect families remain unsupported. Source schema
cross-check: niftools NiPS format documentation; shader texture composition was
checked against our MS2StandardMaterial/Shader0002-P.hlsl. No reference assets
were copied. No commits, deployment, DB writes or existing-process stops in this
effects follow-up. Generated libraries and the Gelo profile remain ignored.

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
dyes RGB63,59,51 and 20,18,15 when choosing the hoodie, which does not establish
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
