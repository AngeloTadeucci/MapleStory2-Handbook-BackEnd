# Model migration handoff to the PC

## Start here

The VM run is stopped. The user authorized commits and pushes to a new branch in both repositories:
`feat/model-migration-pc-handoff`. Nothing was deployed. Bring both branches to the PC;
the frontend and converter changes belong together. Git bundles are available in
`NifToGltf/obj/model-migration/handoff-transfer/`, with commit IDs and SHA-256 hashes
in `transfer.json`. The bundles contain code and documentation, not game assets.

Frontend handoff commit: `b274f08`. The backend commit is the commit containing
this document; exact hashes are also recorded in `handoff-transfer/transfer.json`.

Read [MODEL-MIGRATION-RUN.md](MODEL-MIGRATION-RUN.md) for completed verification and
[MODEL-MIGRATION-PLAN.md](MODEL-MIGRATION-PLAN.md) for the acceptance gates. The plan
is not complete. More disk space enables the full package build, but does not
resolve converter failures, stale simulator entries, or client-parity checks.

## First fix: Olympus Divinity Wings

The live simulator search was checked before stopping the server. Searching
`Olympus Divinity Wings` with `availability=preview` returned zero results;
`availability=all` returned the unavailable records.

- Male item IDs: `11820357`, `11850133`.
- Female item IDs: `11820358`, `11850134`.
- Source model: `11850133_c_mtolympos01`.
- Old wardrobe error: the KF contains two `NiSequenceData` roots named `Idle_A`.
- The current converter supports both roots. The final standalone asset converted
  successfully with `Idle_A [sequence 0]` and `Idle_A [sequence 28]`, both about
  1.333333 seconds. Its manifest has no body binding or equipment attachment.
- The simulator still inherits failed male/female entries from release 15, with
  empty `parts`. It filters unavailable entries out of the normal picker.

This is a missed simulator regeneration step, not a disk-space blocker. Do not
just mark the entries available or substitute the unattached standalone mesh.
Regenerate the body-bound equipment jobs using the updated converter, assemble a
fresh catalog, and verify placement, animation, dye, and the separately recorded
client-effect limitation on both bodies. Inspect other stale wardrobe failures
resolved by the newer converter; this mismatch is not proven unique to wings.

`expand_wardrobe.py run` skips IDs present in existing checkpoints, including
failed jobs. Use a fresh prepared workspace for retries. Preserve the original
`expansion/` and its reports. `prepare`, `run`, and `assemble` have different
arguments; inspect `--help` and the original job plans before selecting a subset.
Preparation/assembly must preserve the reviewed baseline, recovered hair forms,
customization, and patch metadata. Do not overwrite `simulator-release-15`.

## What exists now

- `canonical-all/`: isolated candidate with 13,249 native assets, consisting of
  9,329 retained wardrobe assets and 3,920 additional standalone conversions.
- `all-models/`: selected additional exports and complete conversion history.
  `conversion-coverage.json` contains 575 unresolved source-model records.
- `remaining-model-families.json`: all unresolved records grouped by failure.
  Largest groups are 222 texture effects, 112 missing animation targets, and
  77 embedded material controllers. Missing original sources/textures also remain.
- The simulator catalog separately contains 5,302 unavailable item/body entries.
  Those counts are different units. Standalone conversion does not prove an item
  can attach correctly to a player body or make its catalog entry available.
- `published-mirror/` and `published-backup/`: verified recovery collection and
  original per-object metadata. 118,875 files matched the read-only R2 check.
- `final-package-audit.json`: all final native hashes match zero-error,
  zero-warning Khronos fixtures; no missing packaged buffer/image dependencies;
  all 97,952 retained legacy file hashes and sizes match the verified mirror.
- `final-catalog-audit.json`: no missing part/hair-form IDs in available entries.
- `canonical-all-build-staging/build-input.json`: 114,035 files staged and hashed,
  32,178,438,390 source bytes. The disposable staging links are removed during
  cleanup; the build-input report is retained in `handoff-transfer/`.
- `compiled-flows-final-clean/`: browser evidence for both hair/hat cases,
  RGB dye preservation, PNG export, mobile layout, rabbit clip switching and
  a 15-frame GIF restoring the exact paused pose. No browser/model-request errors.

All successful new conversions remain unreviewed for full client parity. Two
standalone face choices remain ambiguous. Explicit omitted-feature metadata is
retained. Noesis and the legacy viewer fallback are still required.

## Get the code on the PC

Branches in both repositories: `feat/model-migration-pc-handoff`.

- Backend: `https://github.com/AngeloTadeucci/MapleStory2-Handbook-BackEnd/tree/feat/model-migration-pc-handoff`
- Frontend: `https://github.com/AngeloTadeucci/MapleStory2-Handbook/tree/feat/model-migration-pc-handoff`

In each PC checkout, inspect status and preserve local work first. Fetch and
review the new branch. Use an isolated worktree if the existing checkout is busy:

```bash
git fetch origin
git worktree add ../model-migration-review origin/feat/model-migration-pc-handoff
```

Choose distinct worktree paths for the two repositories and keep their relative
layout consistent with the commands below. If using an ordinary clean checkout,
switch to the new remote branch instead. Do not reset or discard PC changes.

Optional incremental Git bundles, commit IDs and SHA-256 hashes are also retained
under the VM's `NifToGltf/obj/model-migration/handoff-transfer/`. These provide an
offline copy of the two commits, not game assets. Fetching the pushed branches is
the normal code-transfer path.

## Transfer the ignored inputs and evidence

Git alone is insufficient. Keep the following on the VM until the PC copy has
been verified. Their paths are relative to `/home/ubuntu/repos/`:

1. `MapleStory2-Handbook-BackEnd/Maple2Storage/Resources/`: extracted NIF/KFM/KF,
   textures, XML, recovered shared animation references, and extraction reports.
2. `MapleStory2-Handbook-BackEnd/NifToGltf/obj/wardrobe/`: inventories, prepared
   jobs, patches, customization, conversion reports, and prior exported parts.
3. `MapleStory2-Handbook-BackEnd/NifToGltf/obj/model-migration/`: final candidate,
   selected/previous checkpoints, converter versions, mirror, recovery metadata,
   validation fixtures, reports, and this handoff's transfer artifacts.
4. `MapleStory2-Handbook/static/gltf/simulator-release-15/`: immutable wardrobe
   baseline and release inventory used to assemble the candidate.
5. `MapleStory2-Handbook/static/resource/`: ordinary UI images needed for browser
   verification. Install frontend dependencies locally rather than copying them.

Use WSL2 on a Linux filesystem. The batch scripts use `fcntl`, hardlinks and
symlinks, so native PowerShell is not a drop-in execution environment. Keep the
same `/home/ubuntu/repos/` layout inside WSL where practical: historical reports
and generated links contain absolute VM paths. If using another layout, rebase
links and create fresh job preparation with the PC paths. Do not rewrite old
checkpoint provenance to pretend it was generated at the new location.

This WSL command preserves hardlinks across the selected trees in one transfer.
Create a writable `/home/ubuntu/repos/` on the PC first. It does not delete files
on either machine. SSH access and the PC transfer have not been tested here.

```bash
rsync -aH --info=progress2 --relative \
  ubuntu@100.118.72.53:/home/ubuntu/repos/./MapleStory2-Handbook-BackEnd/Maple2Storage/Resources/ \
  ubuntu@100.118.72.53:/home/ubuntu/repos/./MapleStory2-Handbook-BackEnd/NifToGltf/obj/wardrobe/ \
  ubuntu@100.118.72.53:/home/ubuntu/repos/./MapleStory2-Handbook-BackEnd/NifToGltf/obj/model-migration/ \
  ubuntu@100.118.72.53:/home/ubuntu/repos/./MapleStory2-Handbook/static/gltf/simulator-release-15/ \
  ubuntu@100.118.72.53:/home/ubuntu/repos/./MapleStory2-Handbook/static/resource/ \
  /home/ubuntu/repos/
```

After copying, repeat with `--checksum --dry-run` and inspect differences. Verify
`canonical-all/model-files.json` hashes using the staging command below, and use
`mirror-local-manifest.json` to verify the recovery copy. Hardlinks save space;
they do not protect against in-place edits or disk loss. Never overwrite retained
outputs. Do not transfer VM `.env`, credentials, SDK caches or `node_modules` as
project setup. Configure local database access separately. No parser/database
rebuild is needed for this migration.

Original archives inspected live outside these repositories in `LithMS2-XML`,
PrivateMaple2 and the KMS data tree. Their extraction/source-path reports are
retained. The extracted resources above suffice for existing prepared jobs;
additional source recovery may require those original archives on the PC.

## Build and verify on the PC

Use .NET SDK 8, Python 3, Node compatible with the frontend lockfile, and pnpm.
The VM used Node 25. Install browser/ffmpeg dependencies for the QA scripts as
needed. On Windows outside WSL use `py`; inside WSL use `python3`. Do not source
`obj/oracle-prep/env.sh` on the PC, since it selects VM-specific tool paths.

From the backend checkout in WSL:

```bash
export HANDBOOK_BACKEND="$PWD"
export HANDBOOK_FRONTEND="$(realpath ../MapleStory2-Handbook)"
dotnet build NifToGltf.Tests/NifToGltf.Tests.csproj \
  -p:BaseIntermediateOutputPath=obj/pc-migration/ \
  '-p:DefaultItemExcludes=**/obj/**' \
  -o "$HANDBOOK_BACKEND/NifToGltf/obj/pc-converter"
dotnet NifToGltf/obj/pc-converter/NifToGltf.Tests.dll \
  Maple2Storage/Resources/Models/Character/female/f_body.nif
python3 -m unittest discover -s NifToGltf/Diagnostics -p test_migration.py
python3 -m unittest discover -s NifToGltf/Diagnostics -p test_kfm_source.py
python3 -m unittest discover -s NifToGltf/Diagnostics -p test_scan_nif.py
```

In the frontend, use `pnpm install --frozen-lockfile`, `pnpm exec prisma generate`,
`pnpm check`, and `pnpm exec vitest run`. Last VM results: 59 native tests,
23 migration tests, eight KFM tests, seven scanner tests, and 387 frontend tests
passed with seven fixture-dependent skips. Typecheck passed with no warnings.

First verify the transferred candidate with a fresh destination:

```bash
export HANDBOOK_MODELS_DIR="$HANDBOOK_BACKEND/NifToGltf/obj/model-migration/canonical-all"
node NifToGltf/Diagnostics/build_wardrobe.mjs \
  NifToGltf/obj/model-migration/pc-stage --stage-only
```

For the full physical production-package build, omit `--stage-only` and use
another fresh directory. Current reserve formula: 8 GiB + 2.5 times all source
bytes. The unchanged candidate requires 89,036,030,567 bytes free, about 82.9 GiB,
AFTER copying the inputs. Allow more for new exports and outputs. The VM had only
about 6 GiB free. The full-copy build is genuinely disk-blocked; the wings retry
is not. The build tool sets `PUBLIC_MODELS_URL=/gltf/` for this local build and
checks the output file hashes against the explicit allowlist.

After rebuilding wardrobe entries, use `package_models.py` with the fresh
assembled release, discovery entries, `--npc all-models`, `--legacy published-mirror`,
and `--database database.json`, writing to a fresh candidate. Rerun catalog,
hash/dependency, validator and browser checks against that exact candidate.
Never edit hardlinked glTF files in place. Re-export affected TCB models using
the current converter; retain `tcb-corrected-jobs/invalidates.json` semantics so
known-bad earlier curves cannot reappear after a failed retry.

A passing build or validator does not establish full visual/animation parity.
Finish the source-feature work, wardrobe retries, and client comparisons before
Noesis removal. Only the new handoff branches are authorized for pushing. CDN changes,
deployment, CORS edits, and live rollback tests remain outside this run.

## VM cleanup

The current review server on port 4011 is stopped. The user's unrelated servers
are preserved. Disposable compiled QA output, generated staging links and
superseded package attempts are removed only after commits and handoff creation.
`handoff-transfer/cleanup.json` records the exact removals and measured disk delta.
The final candidate, baseline release, sources, recovery collection, checkpoint
history and browser evidence remain on the VM for transfer. Do not infer reclaimed
space from `du` alone: many large folders share hardlinked files.

Cleanup completed after the handoff commits. Removed these disposable directories
under `NifToGltf/obj/model-migration/`: `build-app-seek`, `build-kit-seek`,
`canonical-all-build-staging`, `canonical-build-staging`, `canonical-final`,
`canonical-complete`, and `canonical-candidate-3`. The measured free-space increase
was 853,991,424 bytes, about 814 MiB; the VM then had about 6.5 GiB free.
The post-cleanup check found all 114,034 allowlisted candidate files present at
expected sizes. `handoff-transfer/post-cleanup-check.json` records the check.
Earlier byte-hash validation remains documented in the run report; cleanup did
not rewrite retained assets. Git working trees were clean after the commits.
