# Unified item and NPC model migration

Status: all prepared local conversion batches finished on 2026-09-09. Cleanup, recovery, shared rendering, and the isolated canonical package are implemented. The package contains 13,249 native assets, with 575 unresolved model records. The plan is not complete: coverage and client-parity gates prevent native-only cutover and Noesis removal. Results and remaining gates are in [MODEL-MIGRATION-RUN.md](MODEL-MIGRATION-RUN.md). Commits, pushes to new handoff branches, and a PC handoff were subsequently authorized. Nothing has been deployed. No remote assets or database rows have been changed.

## Intended result

- Item detail previews and the Fashion Simulator render the same converted assets with the same material interpretation.
- Convert the complete existing item and NPC model inventory to the native format. Inventory other model consumers before changing shared paths.
- Keep models inside their existing CDN model folders. Remove the simulator release-number path from runtime configuration.
- Store each NPC's mesh, skeleton, and supported authored animation clips in one glTF. Switch animations without fetching another model.
- Export and select the game's alternate hair meshes when equipping hats.
- Keep a complete, verified local copy of the published item/NPC model collection and its dependencies.

## Findings checked during planning

- The current standalone item viewer uses model-viewer. The simulator uses Three.js plus custom handling for dye masks, character lighting, source render state, equipment bindings, and face animation. Changing only the URL will not reproduce the simulator's appearance.
- The converter already supports KFM input with `--clips all`. This is a starting point for NPC batches, not proof that every NPC controller or material converts correctly.
- The wardrobe exporter looks for sibling `_c` and `_d` hair forms. Outlandish Locks, item 10200001, has both source files under `Maple2Storage/Resources/WardrobeSources/Item/0/02/`, but its current catalog entry has an empty `hairForms` map. Determine where discovery, baseline reuse, conversion, or packaging lost those forms.
- Existing simulator assets can share a source model while having different body bindings, equipment placements, or attachment metadata. A source basename alone is not always a unique asset key.
- The current published simulator files use immutable cache headers. Changing paths does not justify retaining those headers for mutable canonical filenames.

## 1. Inventory and recovery preparation

1. Inventory existing R2 model folders and all referenced files, database item/NPC model references, source NIF/KFM/KF files, textures, and current converter outputs.
2. Build a mapping from source model and item/NPC IDs to existing folder, body variant, attachment variant, hair form, and animation clips. Account for aliases and shared model names.
3. List every consumer of those URLs, including item previews, NPC previews, full-screen model pages, GIF capture, and the simulator.
4. Download missing published files to complete the local mirror. Verify paths and hashes; keep experimental and private character files outside the publish set.
5. Before replacement, back up every affected remote object and its serving metadata. Record the previous manifest and application revision for rollback.

Deliverable: inventory, collision report, source-coverage report, and a reproducible backup/restore manifest.

## 2. Shared renderer and asset contract

1. Extract the simulator's material, dye, source render-state, orientation, and animation support into reusable rendering modules.
2. Use a lightweight Three.js item viewer built on those modules. Preserve orbit/zoom, framing, loading/error states, screenshot behavior, and existing page layout. Do not mount the full wardrobe UI on item pages.
3. Distinguish character-bound equipment from standalone previews. Frame visible geometry, not the full skeleton; retain separate animation and attachment handling where required.
4. Define manifest fields for model identity, explicit variants, default standalone asset, clip names/durations, and required auxiliary metadata.
5. Adapt NPC viewers and the shared GIF capture interface to the same model/animation contract. Preserve camera controls, speed, pause, seeking, and static screenshot fallback.
6. Validate representative hair, clothing, transparent accessories, skin-colored equipment, emissive/effect items, and rigid/animated NPCs before switching all consumers.

## 3. Complete conversion and hair-form recovery

1. Reuse already verified native exports when their source and conversion settings match. Convert remaining models from original sources, not from lossy legacy glTF exports.
2. Run a resumable batch with per-model results: successful, source missing, unsupported feature, and visual mismatch. Never count silent omissions as successful conversion.
3. Resolve unsupported features by source/controller family, then rerun affected models. Preserve the old working asset while an individual replacement remains unverified; the migration is not complete while unexplained exceptions remain.
4. Audit every hairstyle's authored alternate meshes, including models reused from the earlier baseline. Trace Outlandish Locks first.
5. Confirm the client's hat-to-hair-form selection from item data and client behavior. File suffixes alone are insufficient evidence of compatibility.
6. Export valid forms, preserve dye and adjustable-hair settings when switching forms, and restore loose hair when removing a hat. Keep an explicit unavailable result only when evidence supports it.

## 4. Combine NPC animation files

1. Resolve each NPC's KFM references and enumerate authored clips, preserving clip names and timing. Handle aliases, duplicated clip names, genuinely separate mesh variants, and missing references explicitly.
2. Export one glTF per NPC model variant with one mesh/skeleton set and all of that variant's clips. Do not concatenate complete scenes once per animation.
3. Check source animation counts, durations, target bindings, interpolation, root motion, material animation, and any effects the current NPC viewer exposes.
4. Populate the animation selector from the combined asset. Selection changes playback on the loaded scene without a new glTF request.
5. Exercise NPC GIF export with the selected clip and restore playback after capture.

## 5. Fold assets into existing folders

- Keep the existing `<model>/<model>.gltf` entry where one canonical standalone model is valid.
- Put required body, hand, or attachment variants beside it using readable, stable filenames, for example `<model>/<model>-female.gltf` and `<model>/<model>-male.gltf`. Determine the canonical choice explicitly; never select an ambiguous variant by array order.
- Keep named alternate hair models in their corresponding existing model folders and reference them from the manifest.
- Put shared manifests and customization data at stable, unversioned paths. Keep per-model dependencies relative to their owning folder where possible.
- Rewrite all references and verify that every buffer, texture, face sequence, background, and effect resolves. Decide embedded versus external resources from measured transfer and reuse costs; folder consolidation does not require duplicating shared textures.
- Remove the release selector and update catalog URLs, API loading, build packaging, and the hat adjustment currently conditional on release-number paths. Represent required compatibility behavior with explicit asset metadata.
- Use revalidation-oriented cache headers for mutable canonical files and manifests, with CDN invalidation during replacement. No release-number folders or timestamp query strings as the normal serving scheme.

## 6. Verification and cutover

1. Validate every generated glTF and dependency; compare source/output geometry, skin bindings, and animation inventories.
2. Add focused tests for manifest resolution, aliases, body variants, hair-form selection, clip switching, and capture restoration.
3. Inspect old/new item previews and simulator rendering across representative material and attachment families. Exercise both bodies, dyes, hats, hair controls, NPC animation switching, PNG/GIF output, and desktop/mobile layouts.
4. Test the compiled application against the prepared normal-folder layout. Require zero unexpected missing assets or console errors. Test NPC clip switching without additional model downloads.
5. Publish changed files in dependency order, verify remote hashes/MIME/CORS/cache behavior, and coordinate canonical-file replacement with the compatible viewer rollout. Avoid a period where the old viewer consumes an incompatible replacement.
6. Commit and push approved application/converter changes, then deploy and verify actual public URLs. No database rebuild by default; use existing item/NPC references plus manifests unless the inventory establishes a specific schema requirement.
7. Retire old per-animation NPC files and the simulator release folder only after consumer references and production checks confirm they are no longer needed. Keep the recovery backup separate from normal serving.

## 7. Remove Noesis after native coverage is verified

1. Make native conversion the default once the inventory and rendering checks pass. Resolve remaining Noesis-dependent models before calling the migration complete.
2. Remove the Noesis conversion path, invocation scripts, binary/package dependencies, and obsolete legacy viewer fallbacks. Update build, deployment, and contributor documentation.
3. Keep historical exports and comparison evidence in the recovery archive, outside the active conversion and serving paths.
4. Verify a clean setup can convert, build, and serve the full supported inventory without Noesis installed.

## Completion criteria

- Every inventoried model has a verified replacement or an explicitly reviewed source limitation; failures are not hidden by a successful batch exit code.
- Item previews and the simulator share rendering behavior and the same underlying model assets.
- Each NPC model variant loads once and exposes its authored clips.
- Supported hats automatically select verified alternate hair meshes, including the reported Outlandish Locks case.
- Runtime URLs use existing model folders with no release-number prefix.
- The local published mirror matches R2, and rollback is documented and tested on an isolated copy.
- Native conversion is the default and the active toolchain has no Noesis dependency.
