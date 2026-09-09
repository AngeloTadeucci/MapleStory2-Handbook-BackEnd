# PC migration run, 2026-09-09

Continuation of [MODEL-MIGRATION-HANDOFF.md](MODEL-MIGRATION-HANDOFF.md).
PC work continued on `feat/model-migration-pc-handoff` in both repositories.
No production, CDN, or database writes were performed during this run.

## Local storage and inputs

WSL was updated from 2.3.26 to 2.7.13. With the user's authorization,
`Ubuntu-24.04` was moved to `E:\WSL\Ubuntu-24.04` using
`wsl --manage Ubuntu-24.04 --move E:/WSL/Ubuntu-24.04`.
The registered BasePath and successful Ubuntu startup were verified afterward.
The Docker Desktop distribution was not moved.

The working Linux copies retain `/home/ubuntu/repos/` paths inside Ubuntu.
Windows checkouts under `D:\Projetos\MapleStory2\Maple2_Codex` contain the
reviewable source changes. Generated assets live in the Linux copies.

The original Windows Resources tree already matched 56,947 VM files byte for
byte. Another 48,045 files, 6,743,661,640 bytes, were missing. No shared resource
file differed. All 14,481 ordinary UI image files already matched. The retained
migration candidate and release-15 baseline were absent from the Windows checkout.

The five handoff trees were copied together with checksum comparison and
hardlink preservation from `oracle-machine`. No files were deleted. Transfer
completed successfully: 234,321 regular files transferred, 43,187,157,289 bytes.
Logical source size was 143,276,853,522 bytes, including hardlinked history.
Credentials, `.env` files, and `node_modules` were excluded.

The final checksum dry run exited successfully across 521,734 paths, including
432,729 regular files. It found zero missing files, zero content differences,
and zero files to transfer. Only three ancestor directory metadata differences
were reported after local builds. The Linux directory permission difference
was restored to the VM's mode 775. All 13 changed source files in each repository
match byte for byte between Windows and WSL; Git file modes were preserved.

Transfer logs and local setup helpers are under the Windows backend's ignored
`NifToGltf/obj/pc-handoff-audit/`. Linux reports are under
`/home/gelo/pc-migration-audit/`. The original VM collection remains retained.

The fresh original-candidate staging check completed successfully in
`NifToGltf/obj/pc-original-stage`: 114,035 staged files and 32,178,438,390 source
bytes, matching the handoff. Its command hashed the complete allowlist before
creating staging links. The recovery audit reuses those verified hashes only
for identical immutable inodes and hashes additional mirror files directly.
All 118,875 recovery files, 21,394,571,143 bytes, match their recorded hashes and
sizes, with no missing or changed files. The audit reused 110,109 verified inode
identities and hashed 8,766 additional inodes. The transferred candidate allowlist
SHA-256 is `52afdb55c532f2b46768f3230cb26a9248a519f644586adee92b3467b83870fb`.

## Olympus and stale wardrobe retries

The two authored Olympus `Idle_A` roots have 27 and 13 tracks. Every shared
track has identical sampled times and values. Both roots have event 0 and the
same duration. The converter now records a preview default only when one root
is the unique strict superset of the others, with matching event, accumulation
root, duration, interpolation and exact track data. Both authored clips remain.
Conflicting, equally complete, or disjoint sequences remain ambiguous.

The exporter, wardrobe animation guard and frontend use the same explicit
`defaultEquipmentClip` metadata. Animation ownership checks remain required.
This structural preview choice is not a claim of full client parity.

Fresh body-bound jobs use the authored `MT_Point01` attachment to
`Bip01 Spine1`. Eight Olympus item/body entries share these exports:
male 11820357, 11820638, 11820644, 11850133;
female 11820358, 11820639, 11820645, 11850134.

The broader retry selected 2,089 stale item/body entries and ran 870 jobs in
`NifToGltf/obj/pc-supported-retry`. Sixty exports succeeded. Of 810 failures,
809 require verified multi-axis item dummy rotation order and one requires
skinned attachment dummy transform support. These failures remain explicit.

Subset assembly now supports `--retain-unselected`. It preserves every
unselected catalog record and saves inherited provenance and failure reports
in the new output. `pc-assembled-release` retains all 33,170 raw catalog entries:
80 verified, 27,806 preview, and 5,284 unavailable. This is 18 fewer unavailable
entries than release 15. Export counts and complete item/body coverage are
different units. Release 15 and original retry checkpoints remain unchanged.

`NifToGltf/obj/pc-canonical` is the fresh packaged candidate. It contains 13,309
native assets and 114,094 allowlisted files, plus `model-files.json` itself.
All 13,249 retained native hashes and 97,952 legacy hashes match the original
candidate. The 60 added native hashes match the validated retry outputs.
The canonical catalog preserves every unselected record and all customization.
No available entry loses a referenced part, alternate hair form, hand part, or
stowed part. Hair recovery preserves the existing canonical availability labels:
79 verified, 27,807 preview, and 5,284 unavailable. Two standalone default choices
remain explicitly ambiguous.

## Verification performed

The full physical build in `NifToGltf/obj/pc-full-build` completed both large
asset copies. The adapter also emits compressed transport files. The original
checker rejected those extra files; `verify_model_package.mjs` now verifies
every original hash and requires every gzip/Brotli sidecar to decode exactly
to an allowlisted original. It rejects other files and non-physical output.
The completed physical package passes: 114,095 originals, 2,864 transport files,
116,959 total files. Five verifier tests pass, including negative cases.

The first compiled browser run passed Olympus search/equipping on both bodies,
hair forms and dye preservation, PNG export, mobile layout, and the NPC GIF
pose-restoration checks. It exposed 161 icon requests using `/gltf/resource/`.
A directly checked icon returned 200 at `/resource/` and 404 under `/gltf/resource/`.
`PUBLIC_IMAGES_URL` now optionally separates image and model origins. Existing
deployments retain the previous base when it is unset; isolated local builds
use `/` for images and `/gltf/` for models.

`build_wardrobe.mjs fresh-work --assets-from=verified-build-directory` supports
application rebuilds with the same immutable inventory. It requires a successful
physical-build receipt and matching inventory, uses a fresh directory, and
verifies all final hashes again. SvelteKit clears intermediate output and
overrides ordinary copy settings, so reuse is applied after compilation and
through a post-config hook. The final model files are hardlinks to previously
verified physical files; ordinary UI assets and application code are rebuilt.
Default builds still perform the full physical copies. Twelve image/adapter
tests pass in WSL, including restoring assets after output cleanup. An additional
migration test rejects reuse without a verified physical-build receipt.

The corrected compiled application is in `NifToGltf/obj/pc-ui-build-2`.
Its complete build command exits successfully, and `build-result.json` verifies
114,095 original files and 2,864 transport files. Every output is a regular file;
all original hashes and all decompressed transport hashes match the allowlist.
`pc-compiled-flows-final/` in the Windows backend contains the passing browser
report and screenshots. Both preview-mode Olympus text searches return the
original reported IDs; both body variants equip through the actual picker.
The rabbit uses one model request and exports 15 GIF frames while restoring the
exact paused pose. Both hair/hat cases preserve RGB dye and alternate forms;
PNG export and the 390-pixel mobile layout pass. No page errors or failed model
requests were recorded. The final local server log contains no icon 404s.

The local compiled review server uses `http://127.0.0.1:4011/outfits` and a
temporary Windows-to-WSL database relay for SELECT queries. It uses the existing
local database; no database permissions, tables, or rows were changed.
The relay and startup helper are in the ignored `pc-handoff-audit/` directory.

- Native build: zero warnings; 59 tests passed. Two optional Sassy/Curly fixture
  checks explicitly skipped because their expected local paths were absent.
- Migration Python tests: 26 passed. KFM tests: eight passed. Scanner tests:
  seven passed.
- All 60 fresh exports: zero Khronos validator errors and warnings.
- Frontend suite with release-15 body animations and the assembled wardrobe:
  228 passed, 22 fixture-dependent skips. Sixty fresh exports additionally passed
  the body-binding suite through idle and run. Its report contains 61 passes;
  the extra neon fixture check returns early when that old asset is absent.
- The inventory test was corrected to compare raw authored catalog entries
  against source inventory. Schema parsing adds 11 separately audited preset
  aliases, which are included in the subsequent paginated picker test.
- The Windows Sassy/Twin preview fetch mocks now map canonical metadata requests
  to their declared release-14 fixtures. All seven tests pass with those local
  fixtures. Their original failures came from fetching absent root metadata.
  After the image/build changes, the Windows suite passes 230 tests with 20
  fixture-dependent skips.
- `verify_olympus_wings.mjs` exercised both actual assembled player bodies with
  the real simulator renderer. Animation and dye change rendered pixels;
  switching authored clips restores the exact pose and dye. Each body requests
  exactly two models. No page or console errors. Screenshots and results:
  Windows `NifToGltf/obj/pc-wings-browser-release15/`.

## Chilly Round Chain Glasses follow-up

Items 11120100 and 11150058 are female EY entries sharing
`11150058_f_eyvalentine03.nif`. The previous binary reproduced the recorded
block-23 `NiAmbientLight` failure. The new reader supports static ambient lights,
and the writer resolves their inherited or explicitly affected subtree scope.
Duplicate references contribute once. The character renderer adds the authored
ambient contribution only to the affected material; unrelated body materials
retain their previous lighting. Animated ambient lights still fail explicitly.
The binary layouts follow [niftools/nifxml](https://github.com/niftools/nifxml/blob/master/nif.xml).

The next failure was block 4, `NiTextureEffect`. This file's reflection has no
affected nodes and no `NiNode.Effects` reference. Its transform is retained and
tagged `nifUnboundTextureEffect`; no reflection shader is invented. Bound texture
effects still fail explicitly. The glasses export contains one mesh, 1,036
vertices, 944 triangles, four textures, and the authored head attachment.
No geometry or attached features were omitted.

The fresh retry is `pc-glasses-retry`, batch `e74df98179ed540cffaa`, using
`pc-converter-glasses-v2/NifToGltf.dll`. Asset
`wardrobe-11516df22de324616a39de19` serves both item identities.
The fresh candidate is `pc-glasses-canonical-2`. It preserves the previous
13,309 native assets and every legacy file, adding the native glasses under
`11150058_f_eyvalentine03/11150058_f_eyvalentine03-female-ey-native.gltf`.
The suffix preserves the existing legacy file at the ordinary model URL.
Exactly two catalog records change, both from unavailable to preview.
Availability is now 79 verified, 27,809 preview, and 5,282 unavailable.

Verification: 61 native tests pass, including source parsing and ambient scope;
231 frontend tests pass with 20 fixture-dependent skips; typechecking has no
errors or warnings; Khronos validation has no errors or warnings. The direct
renderer check exercises the real body and glasses: dye and body motion change
pixels, toggling only the glasses ambient uniform changes pixels, and restoring
it restores the exact image. No page or console errors occurred. Screenshots
were inspected. Evidence is in Windows `NifToGltf/obj/pc-glasses-renderer/`.
Full game-client appearance comparison remains unverified.

The compiled application is rebuilt in `pc-glasses-ui`. Its pre-delta package
receives a full original and compressed-sidecar hash check. The six-file model
delta is then installed by replacing files, never writing through retained
hardlinks. Changed originals are hashed directly; every unchanged file must
retain its verified inode, size, modification time, and change time. Stale
compressed versions of changed metadata are removed. The final build receipt
records this verification method. Setup and packaging helpers and receipts are
retained in the ignored `pc-handoff-audit/` directory.

The final receipt covers 114,096 originals and 2,854 transport files, with
116,944 unchanged files retaining their verified identities. The preview at
`http://localhost:4011/outfits` now serves `pc-glasses-ui`. Both IDs were searched
and equipped through the compiled female Eyewear picker, and PNG export passed.
There were no browser errors or failed requests. Final screenshots were inspected;
evidence is in Windows `NifToGltf/obj/pc-glasses-picker/`. The hidden background
launcher now selects this build. Windows and WSL source copies match.

## Remaining acceptance gates

The build still reports existing SvelteKit asset-configuration deprecation,
custom-output tsconfig guidance, and unused `Object3D` imports. These did not
prevent compilation or the runtime checks. A separate tracking issue for the
build-configuration warnings would keep that cleanup outside the model changes.

Full source-feature coverage and client comparisons remain incomplete. The
575 unresolved standalone source records are separate from the 5,282 unavailable
wardrobe item/body entries. Existing material/effect limitations remain recorded.
Do not remove Noesis or the legacy viewer fallback. Production deployment,
CDN changes, live rollback testing and database rebuilds are outside this run.
