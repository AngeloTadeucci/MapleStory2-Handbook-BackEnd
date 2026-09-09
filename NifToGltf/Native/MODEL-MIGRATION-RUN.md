# Local migration run, 2026-09-09

Local implementation and all prepared conversion batches have finished. The final
package contains 13,249 native assets: 9,329 retained wardrobe assets and 3,920
additional conversions. There are 575 unresolved model records. The user subsequently authorized commits, pushes to new handoff branches, and
a PC handoff. No deployment, CDN object write, or database write was performed.
The migration is not complete: conversion failures and ambiguous standalone
variants remain. The legacy viewer fallback and Noesis default are retained
until the coverage gate passes.

## PC continuation

See [MODEL-MIGRATION-HANDOFF.md](MODEL-MIGRATION-HANDOFF.md) for local commits,
Git bundle transfer, ignored data transfer, the Olympus Wings catalog gap, PC
build commands, and post-handoff cleanup. The review server was stopped at the
user's request. Commit and new-branch push authorization was subsequently given. Deployment
remains prohibited.

## Revisions and evidence

Both repositories were fetched and equal to upstream before work, with no
uncommitted changes. Work began on `feat/clothing-simulator`; the final commits use
`feat/model-migration-pc-handoff` as subsequently authorized.

- Backend base: `dff68b46bba9a358409eb6d8175a6865df4428d4`.
- Frontend base: `46ed7957250ddb96b1b67faaa9d7c546c8771e8d`.
- Published source collection: `simulator-release-15` and existing model folders.
- Generated evidence: `NifToGltf/obj/model-migration/`, ignored by Git.

Keep the evidence directory, original source extractions, and published mirror.
Canonical candidate assets use hard links to immutable input exports. They are
not independent protection against filesystem loss or in-place source edits.
The staging tool rejects an existing destination instead of overwriting inputs.

## Build cleanup and recovery

The user authorized removing the previously identified build copies. Removed:

- `NifToGltf/obj/wardrobe/build-14c/kit`.
- `NifToGltf/obj/wardrobe/build-14c/output`.
- `NifToGltf/obj/release-15-prep/simulator-release-15`.

Cleanup recovered 16,932,241,408 bytes. The release copy was compared against all
12,172 retained frontend release files before deletion. Build inputs, result
reports, source extractions, and retained release assets were preserved.
`cleanup.json` records the operation. Mirror content deduplication subsequently
recovered another 3,569,059,527 bytes, with original per-object metadata recorded
before replacing duplicate files with hard links. A further 896,520,192 bytes
were reclaimed from 243 byte-identical obsolete native exports. Deduplicating
11,908 retained wardrobe build files recovered another 7,499,669,504 bytes. All
were checked against retained release hashes before linking. Superseded QA
application and kit builds were also removed after their replacement passed.

The read-only inventory contains 138,666 R2 objects, totaling 22,570,459,937 bytes.
The selected model/simulator collection contains 118,875 objects, totaling
21,394,571,143 bytes. All selected files are present in `published-mirror/`.

- `mirror-check.txt` and `mirror-check.log`: rclone compared all 118,875 files
  against R2, with zero differences.
- `mirror-local-manifest.json`: local SHA-256, byte sizes, original modification
  times, and extended attributes for every selected object.
- `mirror-seed-remote-metadata.jsonl`: separately captured remote hashes and
  serving metadata for all 12,172 files reused from the retained local release.
  Their MD5 hashes were checked again after metadata capture.
- `published-backup/backup.json`: complete content and serving-metadata manifest.
  Its `objects` directory points to the retained mirror.
- Dependency audit: every external buffer and image URI in the mirrored glTFs
  resolves inside the mirror. Zero missing dependencies.
- Restore rehearsals: the original 12-object Outlandish set and a second five-
  object sample restored and passed content checks. The second also verifies
  per-object metadata restoration after hard-link deduplication.

`migration_recovery.py` has no remote upload or restore operation. Restoration
checks all source hashes before creating a fresh local destination. A complete
local restore needs additional space for a separate copy. Bucket CORS and remote
canonical replacement have not been tested or changed.

## Sources and conversion

The database snapshot uses a read-only transaction: 39,553 items and 7,996 NPCs.
`model-mapping.json` preserves shared database identities and source/asset
collisions. The initial extraction lacked matches for 1,557 NPC names and 76
item names. That initial NPC count is superseded by the archive extraction below.

An existing original NPC archive was found in `LithMS2-XML/out/Resource/Model/`.
34,633 NIF/KFM/KF files were extracted into `Maple2Storage/Resources/NpcSources`.
Archive indexing, extraction hashes, KFM references, and per-job source hashes
are retained. A second archive in PrivateMaple2 was inspected read-only for the
remaining unmatched names; its matching model names are recorded separately.

The source preparation now creates 1,554 NPC jobs and records six unmatched
exact database model names in `npc-source-latest/source-failures.json`. Four have
similarly numbered but differently named assets in both inspected archives.
Two include materially different model or color names. These are not silently
substituted. The two remaining names were not found in those archive searches.

KFM parser fixes consume authored transition text-key string pairs and fixed
integer/float transition records. This enumerates clips; it does not implement
the game's transition graph. Explicit source-root bounds allow shared relative
references without permitting arbitrary filesystem traversal. The shared male
sword idle clip was byte-identical in the player animation extraction and two
neighboring NPC sources before being linked into its authored reference path.

The native animation reader now samples time-adjusted Kochanek-Bartels quaternion
and vector curves. Tests cover quaternion sign equivalence, endpoint easing,
uneven key timing, and vector tension. This does not establish full client
animation equivalence. Sampling errors retain explicit diagnostics and strict bounds.

`npc-groups-v4/` contains the completed resumable 1,550-model conversion run: 1,280
converted and 270 rejected before targeted retries.
`npc-shared-reference-jobs/` contains the four additional shared-reference models,
all converted with converter v5. Each checkpoint records source provenance,
converter hash, texture hashes, output hash, and individual failure details.
Successful conversion is labeled `converted-unreviewed`, not visually accepted.

Remaining failure families observed include missing animation target nodes,
unsupported material controllers, invalid
animation values, quadratic quaternion curves, and
ambiguous texture candidates. No partial failed model is promoted.

Converter v6 preserves multiple authored NiSequenceData roots and KFM events
instead of collapsing duplicate names. Animation extras record the source
sequence block and event. Two targeted sequence retries converted 28 additional models.
Together with the four shared-reference models and five recovered NPC texture
families, 1,317 NPC models converted before the complete published-inventory pass.

The Map, Item, and Effect archives recovered 69 additional exact database source
names. Itemmodel XML paths distinguish 24 Map props from differently authored
Effect models with the same basename. The XML hashes and selected paths are in
`recovered-source-path-evidence.json`. Targeted texture extraction and retries
converted 52 recovered models. The soul-bead NPC source is now present but its
particle-force animation evaluator remains unsupported. Five NPC names and five
item names remain unmatched. The five item names were also searched in the KMS
and PrivateMaple2 Item archives without an exact match.

`migration_standalone.py` preserves whole original geometry and authored external
animation references. It rejects distinct source variants and unresolved clip
mappings. All 696 prepared equipment jobs converted after adding the ten missing
instrument texture paths. Both ambiguous faces remain explicit limitations.

Reference comparison is still an acceptance blocker. Four Balrog TCB clips have
all reference target tracks, but sampled rotations and translations differ from
the published export. Native TCB values match inspected source keys much more
closely on the two inspected tail tracks. Other large differences affect B-spline
tracks. This does not prove either complete export correct. See
`balrog-tcb-comparison.json`, `balrog-tcb-source-key-check.json`, and
`balrog-reference-difference-channels.json` for the measured channels.

Invalid source data was also inspected directly: Rari has active scale tracks
with NaN keys, and Anos has invalid compressed rotation parameters. Missing
binding targets are not broadly ignored, and rejected clips are not silently
removed to make a model count as converted. Supporting these families requires
client behavior evidence and focused converter work before native-only cutover.

The complete mapping exposed 2,160 additional source models outside the first
wardrobe and database-NPC batches. `migration_inventory_jobs.py` prepares these
published and database references, including aliases whose KFM declares another
NIF basename. `inventory-remaining-jobs/` completed with 1,796 converted and 364
rejected before targeted retries. Its source report records nine additional
limitations. Missing original clips were searched in the Item archive without
substituting differently named animations.

Sampling diagnostics identified a defect in the new TCB implementation: .NET's
shortest-path Slerp can flip the SQUAD control arc and introduce a discontinuity.
Original quaternion keys are still hemisphere-aligned once; interpolation of
the resulting controls now preserves their oriented spherical arc. The real Alon
hair track passes a continuity regression and matches the published reference
within 0.00018 degrees at its keys. Three diagnostic models now convert and render
in Chromium without errors. A fourth passes interpolation but lacks a bone
target. Other reference-track differences remain, so this is not full model
acceptance. `tcb-correction-review.json` records the evidence.

The source audit selected 405 models for corrected TCB re-export, now complete.
343 converted and passed Khronos validation with zero errors or warnings. Three
remaining dagger-family models have effectively antipodal TCB controls; they
remain rejected instead of choosing an arbitrary interpolation arc. No sampling-
tolerance failures remain in the final report. The audit includes
older KF files supported by the native converter. `invalidates.json` prevents
known-affected earlier exports from being restored if the corrected conversion
fails on a different feature. All previous output and failure history remains
available outside the candidate.

Six original 30.1.0.3 mannequin NIF files use the supported block layouts. Their
vertex and triangle counts match all six published models. Native reading now
accepts that geometry version, and its binary header must still match the text
header. Header-altered layout probes are isolated under `older-nif-layout-probe/`
and are never collection inputs. Actual retries use the original source files.

## Hair forms and canonical packaging

Baseline reuse discarded discovered hair-form mappings. The exporter now merges
successful discovered forms without discarding reviewed forms or working loose
hair. Failed forms retain explicit reasons. Four hairstyle entries recovered
eight authored forms, including Outlandish Locks. Recovered entries are marked
preview until their combined hat behavior has been exercised.

`package_models.py` stages existing model folders and stable variant filenames,
using explicit canonical choices. NPC controller aliases retain their own model
identity even when they share a NIF. Shared manifests use unversioned paths.
`model-files.json` is the explicit publish allowlist and records revalidation
cache policy for mutable canonical files. It is not a deployment command.

`migration_collect.py` checks checkpoint hashes and retains both successful
exports and failed-retry history. `additional-final/` combines the first 2,065 successful
NPC, equipment, and recovered prop exports. It is superseded by the broader
inventory and TCB correction work described below. The optional `--legacy` and `--database` packaging
arguments retain published fallback models, per-animation files, and their
dependencies until acceptance permits retirement. Private and release folders
are excluded. `legacy-retention.json` lists retained files and missing folders.

The first canonical validation covered 9,330 glTF files: zero errors and zero
warnings. The original candidate had 700 ambiguous standalone choices. Whole-source NIF
exports now resolve 696 equipment models, including multi-part clothing. The
explicit body identities resolve two more. Two face-preset choices remain
ambiguous. All character-bound catalog parts remain separate and retain their
explicit IDs. Canonical staging is a review candidate, not an accepted rollout.

## Shared frontend renderer and checks

The frontend now has a reusable standalone Three.js scene using the simulator's
material, dye, source render-state, lighting, and face-animation support.
Visible geometry determines framing. Native item, NPC, and full-screen previews
use this scene. Models without a resolved native manifest entry retain the existing viewer
fallback. Manifest selection is not a visual-acceptance gate; publishing this
candidate would expose its unreviewed conversions, which is why cutover remains
blocked.

Playback supports clip selection, pause, speed, seeking to zero, screenshot,
and capture restoration. NPC GIF capture uses the native capture interface.
Manifest fields describe canonical identity, variants, clip durations, face
presets, and hat compatibility. Runtime catalog paths no longer select a
numbered simulator release. Build packaging uses the explicit model allowlist.
Historical release files remain available for recovery and existing consumers.

Verification completed:

- 59 native converter tests with real female, rabbit, and duplicate-sequence
  armor-pig source fixtures.
- 23 migration tests, eight KFM source tests, and seven NIF scanner tests.
- Final full frontend suite: 387 passed, seven fixture-dependent tests skipped.
  The seek-axis adjustment was typechecked, built, and exercised in the compiled
  UI. `/tmp/migration-frontend-complete-tests.log` records this run.
- Frontend typecheck: zero errors and zero warnings.
- Production builds pass using isolated build directories.
- All 9,330 initial canonical glTFs pass Khronos validation without warnings.
- Six standalone scenes render in Chromium with zero browser exceptions.
  Clip switching/capture loads one glTF per scene and restores playback.
- The compiled flow in `compiled-flows-seek-3/` passes with zero browser errors
  or failed model requests. It checks male Outlandish and female Bubbly Wave hair,
  Beginner Fishing Hat selecting the exact C-form model, RGB dye persistence
  across hat changes, removal restoring loose hair, PNG downloads, both bodies,
  and a 390-pixel mobile layout without overflow.
- Rabbit NPC playback selects Attack_01_A, changes speed to 2, pauses and seeks
  to zero using the actual clip duration. Local GIF encoding produces 15 frames
  and restores the exact paused PNG pose and speed. Only one glTF is requested.
- Additional Chromium checks render Balrog with 66 clips, the normal doctor with
  five clips, and whole-source clothing. Screenshots were inspected.

The final compiled check against `canonical-all/` also passes.
`compiled-flows-final-clean/` records zero browser errors and failed model
requests, both hair/hat cases, dye persistence, PNG downloads, mobile layout, and
a 15-frame rabbit GIF with exact playback-pose restoration. The reused QA build
initially contained stale precompressed metadata; those generated files were
removed before this successful run. The first attempt preceded server readiness.
Neither failed QA attempt is counted as acceptance evidence.

The compiled QA build registers the catalog metadata as static assets and serves
canonical geometry through local links. The full production package copy has
not been exercised. API POSTs are intercepted during browser checks. GIF frames
are encoded locally with ffmpeg; the external GIF service is not invoked.

## Remaining acceptance gates

`all-models/conversion-coverage.json` retains all checkpoint histories, explicit
TCB invalidations, 3,920 selected successes, and 575 unresolved records.
`remaining-model-families.json` groups every unresolved record. Main groups:

- 222 texture-effect models and 77 embedded material-controller models.
- 112 models with missing animation binding targets.
- 32 non-transform evaluator models and four path-evaluator models.
- 34 records with invalid animation values or geometry normals.
- 12 missing textures, 11 missing source models, and five source-review cases.
- Remaining cases include billboard/camera/light nodes, ambiguous resources,
  empty geometry, quadratic quaternion curves, and three antipodal TCB controls.

These need source/client behavior evidence and additional converter implementation.
The next bounded work is to inspect representative texture-effect and material-
controller sources against the client, specify how their animated properties map
to the shared renderer, and implement those properties with focused fixtures.
Missing binding targets need a separate client comparison before deciding whether
any are safely ignorable. Missing textures/sources need exact archive matches.
Do not drop unsupported blocks or clips merely to clear the coverage report.
Successful exports remain `converted-unreviewed`; schema checks and selected
browser fixtures do not establish whole-inventory client parity. Two standalone
face choices remain ambiguous. Existing catalog availability and omission
metadata are retained, including 5,302 unavailable item/body entries; these
entries are not interchangeable with the 575 unresolved source-model records. Full geometry, materials, animation, and hair/hat acceptance
remain required before changing the native-only default or removing Noesis.

`canonical-all/` is the final isolated review package, including 97,952 retained
legacy files. Its allowlist covers 114,034 files before the allowlist itself.
64 referenced model folders were absent from the selected published mirror;
`legacy-retention.json` lists them explicitly. This is a fallback-availability
limitation, not evidence that those models never existed.
`final-catalog-audit.json` checks all 33,170 catalog entries and finds no missing
native part or hair-form IDs among available entries.
`final-package-audit.json` confirms all 13,249 final native glTF hashes match
fixtures that passed Khronos validation with zero errors and warnings. It also
checks every packaged glTF buffer/image dependency, with zero missing files,
and compares all 97,952 retained legacy file hashes and sizes against the verified
mirror, with zero mismatches. This validates file integrity and glTF structure,
not client rendering parity for every model.

All conversion, packaging, staging, validation, and browser jobs from this run
have finished. The isolated QA server has been stopped.

The final package remains isolated. The user's running application was not
restarted, and its static tree was not switched to this final candidate.
Earlier `canonical-final/` and `canonical-complete/` are superseded incomplete
package attempts, not rollout artifacts. Converter versions and source/report
history must remain available for checkpoint provenance.

Hash-checked staging passed for all 114,035 files, including the allowlist itself.
`canonical-all-build-staging/build-input.json` records its SHA-256 and 32,178,438,390
source bytes. No private/release folders enter the allowlist. The stage uses local
links and is not a physical production copy.

A full production package copy has not run because the disk reserve cannot fit
it. The rule requires 89,036,030,567 free bytes, about 82.9 GiB; the check recorded
6,659,665,920 free bytes before staging. Metadata-only production compilation and browser checks are separate from
that packaging gate. Bucket CORS, remote replacement, and rollback against the
live CDN remain untested. Pushes are limited to the new handoff branches. Deployment remains prohibited.

## Continue locally

```bash
source NifToGltf/obj/oracle-prep/env.sh
python3 -m unittest discover -s NifToGltf/Diagnostics -p test_migration.py
python3 -m unittest discover -s NifToGltf/Diagnostics -p test_kfm_source.py
python3 -m unittest discover -s NifToGltf/Diagnostics -p test_scan_nif.py
```

Do not rerun completed conversions merely to refresh reports. After source or
converter changes, rerun affected models into fresh output and retain previous
reports. Use `HANDBOOK_MODELS_DIR` with `build_wardrobe.mjs` to build the isolated
candidate without modifying another application's static tree.
