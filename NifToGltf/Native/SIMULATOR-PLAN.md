# Clothing simulator completion plan

Current handoff, 2026-09-07: the user authorized committing and pushing candidate
14 on the existing feature branches. Oracle preview servers on ports 4000, 4002
and 4003 are stopped. No deployment is authorized. See backend STATUS.md for
current testing instructions; server-running and no-commit statements below are
historical checkpoints superseded by this handoff.

Implementation checkpoint 2026-09-07: complete inventory/discovery is implemented,
including previously omitted slots, missing DB/name records and explicit exclusions.
All 10,464 model jobs were attempted. Candidate 14 contains 9,329 assets and 7,078
usable source bundle families. Its 33,170 searchable pairs retain 80 inherited
reviewed labels, 27,788 previews and 5,302 unavailable reasons. Source/default,
material/texture provenance, fitting and ordinary animation repairs are implemented.
Final asset suites, validators, build-14c and both-body public browser workflows
pass. Contact sheets cover 77 inspected pairs across 42 cases, with no new visual
promotions. The compiled candidate runs on Oracle at 127.0.0.1:4003/outfits.
Read STATUS.md and Diagnostics/wardrobe-release-report.json for exact coverage,
commands and unresolved source behaviors. This is a concrete review candidate;
complete visual acceptance and the recorded compatibility blockers remain open.
Publishing is unauthorized.

Updated 2026-09-07. The wardrobe expansion below is the current implementation
plan. It takes priority over save/load and sharing. Earlier implementation
phases and release counts below are historical context, not unfinished basics.

Oracle preparation completed on 2026-09-07. Both feature-branch checkouts, matching
KMS2 sources, baseline releases, private Gelo snapshot, build tools and a loopback
review build are staged under `/home/ubuntu/repos`. Read the first STATUS.md
checkpoint and source backend NifToGltf/obj/oracle-prep/env.sh before work there.
249 frontend, 35 C# and 14 Python tests pass; both complete outfit smoke flows were
rendered. This is environment readiness only. That preparation checkpoint preceded the implementation recorded above. The existing database must remain read-only, and preview labels
retain their current meaning. Windows/Linux PNG compression and tiny body float
differences prevent byte-identical cross-platform reexports; retain source and
platform provenance with every new review. No deployment or overnight agent was
started by the preparation request.

## Current plan: complete clothing and decoration coverage

### Outcome and boundaries

Make every clothing and wearable-decoration option in our selected client data
discoverable, and make every supported source family usable on its eligible
bodies. Missing items must be individually accounted for. A successful batch
conversion does not establish correct appearance or finish this work.

Include normal equipment and cosmetic outfits, tops, bottoms, full outfits,
hats, gloves, shoes, back items, hair, faces, makeup and visible accessories.
Inventory all client-declared wearable slots, including slots the current
SLOTS allowlist omits. Include handheld equipment/decorations, distinct hand
placements, and ordinary animated geometry attached to an item. Determine from
client data which rings, belts or other stat slots have a visible component;
explicitly classify nonvisual equipment rather than inventing geometry.

Badges remain excluded. Preserve the supported hair-twinkle effect, but do not
expand into a general effect/particle engine. An otherwise usable garment with
an unsupported effect should identify that omission separately. NPCs, maps,
global Noesis replacement, new backgrounds, an exhaustive pose library, accounts
and outfit save/share are not part of this expansion. Custom player UGC images
require their real supplied content; ship neither invented replacements nor
private player artwork harvested from a database.

### Baseline and accounting

Release-05 currently has 153 models and 127 item/body entries: 82 reviewed,
35 previews and 10 unavailable. These are subset counts, not total wardrobe
coverage. The complete client denominator is unknown until the inventory runs.
Keep release-05, its 635-file inventory and Gelo's private preview intact.

Count distinct item IDs, eligible item/body pairs and reusable asset families
separately. A unisex item may have two body entries. Shared geometry must never
make a real item ID disappear. Track source presence, conversion status, visual
review and optional-feature support separately. The existing UI availability
labels can remain, with precise reasons derived from that richer evidence.

### 1. Inventory the full wardrobe before selecting batches

- Read the installed client itemdata, itemmodel/preset references, customization,
  archive indexes and relevant feature/locale selection. Itemdata identity and
  eligibility must be joined to the selected model; an itemmodel ID or filename
  alone is not a complete list of equippable items. Preserve source hashes.
- Extend Diagnostics/simulator_library.py beyond its fixed per-slot sample,
  explicit ID list, slot allowlist and filename-based gender heuristics. Resolve
  exact paths and URNs using archive metadata; record ambiguous references.
  Do not skip missing parts, mixed slots or items without geometry without a row.
- For every item/body pair, record selected preset, all parts, attachments,
  cutting, dyes/defaults, hair forms, private joints, animation/material needs,
  source provenance and blockers. Classify exclusions and nonvisual equipment
  with a reason. Record client/Handbook database mismatches without DB writes.
- Produce wardrobe-inventory.json, coverage-by-slot/body and a failure-family
  report. Every source item in the selected scope must reconcile to inventory
  or an explicit exclusion. Report missing names/icons separately from geometry.

Exit: a deterministic complete denominator, no silent drops, and an ordered list
of conversion families. Resolve scope classification from the client before
treating a slot as unsupported. Do not estimate a completion percentage from 127.

### 2. Make the full inventory searchable

- Extend the catalog producer, frontend catalog.ts, outfit API and outfit page
  together. Include source-backed entries absent from the Handbook DB or with
  empty names. Prefer the existing localized DB label when present, otherwise
  a source label or explicit item ID. Missing icons get a visible placeholder.
- The current API filters by database slot/gender and nonempty name, and only
  recognizes slot-0 full outfits through selected library IDs. Replace those
  coverage assumptions with the reconciled inventory's item/body/slot data.
  Ensure filtering and pagination use one consistent dataset and count.
- Keep reviewed-only and available-model browsing; All clothing must include
  unresolved entries with concise reasons. Failed conversion must not remove an
  item from search. Show effect-only omissions independently of equipability.
- Keep index loading bounded and models lazy. Use a versioned metadata index,
  paginated queries and caching as needed; do not load the wardrobe's geometry
  to search it. Retain parameter validation and read-only database access.

Exit: every in-scope ID is findable on its eligible bodies, including unresolved
and DB-missing cases. Name, ID, slot, body and availability pagination reconcile
to inventory totals. A catalog completeness test must enumerate all records.

### 3. Convert by shared source family

- Generate batches from inventory, not hand-maintained lists of favorite IDs.
  Reuse NativeBatch, ItemModelAttachment, SkeletonGraft and existing texture/
  archive tools. Retain exact item-specific metadata around reusable geometry.
- Cache by all conversion-affecting inputs: source model/texture hashes, body
  skeleton, attachment/transform choices, source options and converter version.
  Do not deduplicate items or declare two bundles equivalent merely because
  their NIF path matches. Customization and dye differences remain explicit.
- Use deterministic, bounded, restartable batches with per-item failures.
  Write to fresh work/candidate paths. Fix high-coverage failure families and
  retry only affected inputs; do not rerun unchanged full archives repeatedly.
- Queue ordinary tops/pants/full outfits and footwear/gloves first; then hats,
  hair/faces/makeup and accessories; then back items, multi-part decorations and
  hands. Within each group, prioritize fixes that unlock the most eligible
  entries and fill both-body gaps. Do not treat the first useful batch as done.

Exit: every resolvable family has been attempted. Remaining failures have exact
IDs, source evidence, a reproducible cause and a next action. Known-supported
families must not retain unexplained conversion failures.

### 4. Resolve equipment and appearance-family blockers

- Triage the existing 35 previews and 10 unavailable entries alongside new ones.
  Prioritize missing textures, incorrect item/preset aliases, private joints,
  exposed skin seams, alpha/material failures and unhandled attachment rules.
- Cover complete multi-slot bundles and rollback on partial failure, robe versus
  top/pants conflicts, exposed-skin dye inheritance, head/face accessories and
  hair A/C/D transitions. Preserve hair length and dye state through hat changes.
- Handle source-defined drawn/stowed/left/right parts separately. Reproduce the
  paired-star stow overlap before claiming a fix. Do not shift items arbitrarily,
  substitute another item, resize the body or silently freeze moving geometry.
- Keep a real failing fixture and add the smallest meaningful regression for
  each new rule or decoder fix. Limit renderer work to behavior required by the
  wardrobe, preserving the accepted shader and hair effects elsewhere.

Exit: each blocked family either works with evidence or has a precise supported
scope and visible limitation. Converted-but-unreviewed is unfinished review work,
not an acceptable permanent failure category.

### 5. Review the expanded wardrobe in complete outfits

- Generate front/side/back contact sheets for each distinct visual bundle and
  eligible body. Reuse review evidence only when geometry, textures, materials,
  skeleton/attachments, customization and cutting signatures are identical.
  Different textures or dyes can expose different failures and require coverage.
- Use T3 and the existing local preview for direct interaction. Review newly
  implemented source families, every exceptional item, and every changed old
  failure. Contact sheets supplement, rather than replace, live checks.
- Use representative pairwise outfit combinations across compatible slots and
  target known seams/conflicts. Exercise fitting/idle and a relevant moving pose,
  equip/replace/remove, both bodies, dye/reset, hair/hat fitting, expressions,
  load failure recovery and PNG export. Exhaustive cross-products are not needed.
- Keep Gelo's twelve saved instances and a complete male outfit as fixed
  regressions. Record exact item IDs, body, hair form, dyes, pose/time, camera,
  background, renderer version and hashes with each review. Check resource
  cleanup and narrow-viewport catalog interaction at the expanded index size.
- Compare hunya only for a specific unresolved behavior with matching IDs/states,
  especially fitting, slot conflicts, expressions and dye defaults. Confirm
  against our XML/NIF/HLSL. If unavailable, record that limit. Use our own assets.

Exit: no family is promoted from conversion/typecheck success alone. Known broken
geometry cannot appear as reviewed or as an unexplained available preview.
Allowed optional limitations remain labeled and attributable to exact items.

### 6. Prepare the expanded release for review

- Build a fresh versioned candidate from inventory plus approved asset/review
  records. Preserve review hashes only when their actual inputs still match.
  Avoid another chain of manual one-off release patches as the library grows.
- Reproduce the candidate, verify all relative URLs/dependencies and inventory
  hashes, and test producer/consumer contracts. Publish no private snapshots,
  raw client archives, temporary captures or failed intermediate outputs.
- Measure index bytes, largest outfit download, initial/equip latency and memory
  on actual desktop/narrow viewports. Compare to the release-05 baseline before
  choosing practical budgets. Deduplicate textures/assets where safe and ensure
  ordinary browsing does not download the entire library.
- Build the frontend against the candidate layout and verify packaged files.
  Update STATUS.md with final totals by slot/body, reviewed/limited/blocked counts,
  remaining exception IDs and evidence. Prepare source and asset release together;
  changing simulator-release.json alone does not distribute the generated files.

Exit: all scoped clothing is discoverable; all items in supported families are
usable and reviewed; remaining individual exceptions are explicit. Both core
body/outfit workflows pass. No broad supported slot may be omitted as an
individual exception. Hand over a concrete release for publishing review.

### Execution and verification rules

Work on feat/clothing-simulator in both repositories. Inspect/fetch upstream and
preserve other changes before implementation. Do not commit, push, deploy, alter
production data, start another preview server or stop existing processes without
new authorization for that action. Current authorization is to write this plan.

Implement in the order above once requested. Update inventory and STATUS.md
after each batch with counts and evidence. Test exact new inventory/resolution,
deduplication, API, equipment and material rules. Run source-backed C#/Python
checks when those components change, focused frontend tests, typecheck, direct
T3 acceptance and final build/package checks. Do not rerun unrelated suites or
claim full appearance parity from automated checks.

## Earlier implementation checkpoints

The user-requested surface shading correction is implemented: source ambient
coefficients, independent material ambient, authored rim parameters and hair
direction-map highlights. Gelo and a complete male outfit passed direct angle,
hat-fitting and dye checks. STATUS.md records exact items, source extracts,
eleven GPU checks, PNG evidence and remaining running-client parity limits.
The accepted hair effects are unchanged.

The later effects request supersedes the previous blanket exclusion. The
hair-twinkle pilot is implemented in release-05 for four source-selected hairs,
with sparkles/glow, an effects toggle and tests on both bodies. Badges remain
excluded. STATUS.md records source bindings, exact visual states and unverified
client parity. This does not expand the release gate to all effects or NPC/maps.

The four authorized follow-ups are implemented in `simulator-release-04`:
source sign animation; star idle/run and explicit draw/stow; 24 more reviewed
item/body combinations, cape recovery and corrected knuckle attachment; client
half-Lambert and source gloss/specular behavior. The catalog's slot-0 full-outfit
filter and camera framing were also corrected after direct interaction.
The 144-model release has 82 reviewed, 32 preview and 10 unavailable entries.
Its 620 files reproduce byte for byte. STATUS.md records exact tests and T3
states. Gelo is `/outfits?preview=gelo-05`. Paired star back overlap, full shader
parity and cape cloth physics remain explicit limits. Effects remain excluded.
Earlier static-sign and baseline-only-star limitations below are historical.
The production build passes with all 620 packaged release files hash-checked.
Candidate libraries and local character snapshots are excluded from that package.

Release-03 checkpoint: Gelo's missing ordinary equipment is present in
`/outfits?preview=gelo-02`: sign 11820024 with its own joints, independent
left/right stars 13400306 with saved dyes, and source UV blush 10400108.
The extended default library `simulator-release-03` has 142 models and 58
reviewed item/body entries. It preserves release-02 and includes a repeatable
extension builder and hash-bound review. STATUS.md records exact outfit states,
tests and direct T3 interaction evidence. These follow-up changes are uncommitted.

Remaining limits: shader parity, the sign's independent wing animation and
weapon combat poses. Dance T intersects drawn stars with the face and is marked
in the UI. These are not silently counted as verified appearance. Effects stay
excluded. Publishing remains a separate authorized action.

Gelo follow-up: a development-only preview at `/outfits?preview=gelo-01`
loads eight saved appearance items and colors from a read-only snapshot of
local `tria-game-server`. Front/side/back views and visible-tab playback were
checked in T3. Explicit omissions and exact evidence are in `STATUS.md`.
The preview is separate from the unchanged release-02 publishing candidate.

User review on 2026-09-06 accepts Gelo's appearance for now, with shader
differences explicitly unresolved. Commit this implementation checkpoint and
plans. Shader refinement and the documented missing Gelo items remain follow-up
work; effects remain excluded. Publishing still requires separate authorization.
Generated libraries, source extracts and the saved character snapshot remain
local ignored artifacts; the source commit does not publish or bundle them.

## Implementation progress, 2026-09-06

Phases 1–3 are implemented in the local `/outfits` route: paginated database
catalog, exact XML item/body bundles, transactional multi-slot equipment,
body/garment cutting, authored cap hair variants, source face textures and blink
timings, palettes and shared skin. The default catalog has 23 reviewed items per
body. Available-model browsing also includes previews, 47 named items per body
in the current database. All-item browsing explains absent or rejected models.

Phase 4 has a recorded T3 matrix for both bodies: hoodie and leather sets, robes,
accessories, all twelve supported slots, and all six baseline clips. Front, side
and back views were inspected. Female 10200224 + cap 11300002 uses its own C
hair model and restores loose hair, dye and selected morph lengths on removal.
Eight cap round trips returned to 26 geometries / 65 textures. An intentionally
missing second robe part retained the complete previous outfit. Robe 12200001
replaces CL+PA together. Current expression choices come from the exported face
sequences; an erroneous hardcoded Sad choice was removed after browser testing.
Three own backgrounds load, dyes and reset were exercised, PNG export was
decoded and inspected, and the 390px layout has no horizontal overflow.

Phase 5 has a self-contained local candidate at
`../MapleStory2-Handbook/static/gltf/simulator-release-02`: 124 models, separate
catalog and customization contracts, own face/background textures and inventory.
There are 46 reviewed item/body entries, 54 previews and 12 unavailable entries.
`Diagnostics/simulator-review.json` records exact IDs, states, evidence and
reviewed hashes. `Diagnostics/build_simulator.ps1` reproduced all 552 inventory
entries byte for byte. The frontend production build passes. Hosted-origin
checks remain part of the separately authorized deployment. The user authorized
source commits after reviewing Gelo. No deployment or production data write has
occurred.

Verification: 31 source-backed C# tests, 11 Python tests, 156 focused frontend
tests, zero typecheck errors/warnings, and 124 glTFs with zero validator errors
or warnings. See STATUS.md for the evidence and precise acceptance limits.

## Outcome and scope

Finish a usable Handbook clothing simulator at `/outfits`: browse clothing,
assemble a character, customize its appearance, choose a pose/background and
export an image. Both body variants and the core clothing slots must work.
Some individual clothes, poses and effects may remain unavailable.

Simulator release does not require complete Noesis replacement for every NPC,
map or clothing asset. Effects and particle simulation remain excluded. Missing
assets must be visible in coverage reports and handled honestly in the UI.
Do not render broken or incompatible items as though they were supported.

This plan owns the next implementation order. It supersedes the requirement in
the original CONTINUE prompt to finish the entire non-effect library before
shipping the simulator. The wider converter work remains documented in Plan 09.
Deploying the site or assets is a separate step requiring authorization.
Do not commit unless asked.

## Foundation before this implementation

- Native NIF geometry, skinning, texture decoding and merged animation exports.
- Both player bodies with six curated clips each.
- Actual item/NPC viewer integration through native manifests.
- `/outfits` uses one body skeleton and shares its joints with selected gear.
- Source XML attachment metadata, clothing-part replacement and skin inheritance.
- Source-mask recoloring, shared skin controls and reset.
- Orbit camera, animation selection, play/pause and PNG capture.
- Acceptance-06 has twelve exports: two bodies, eight equipment variants,
  one NPC and one map object. This is an acceptance sample, not a clothing catalog.
- Latest checks: 31 C# tests, nine Python tests and 28 focused frontend tests.
  Frontend typecheck passes. Twelve acceptance exports validate without warnings
  or errors. The latest broad static batch has 2,626 valid exports, one excluded
  effect, two missing inputs and 201 failures; it predates recent simulator work.

The yellow female test garment was unsuitable for appearance acceptance. The
staged male event shirt has a wrist shape incompatible with the selected body.
Both remain documented; selecting a different sample did not fix those assets.
See [STATUS.md](STATUS.md) for evidence and the remaining converter limitations.

## Phase 1: Catalog and supported clothing library

Replace the fixture-ID dropdowns with a searchable item catalog.

1. Read existing item fields: id, name, icon_path, slot, gender, is_outfit,
   dyeable and kfms. These fields exist in the inspected Prisma schema; verify
   the actual query results during implementation. Do not invent schema fields
   or run the parser/database rebuild merely to populate the simulator.
2. Add a focused, paginated outfit search API, or extend the existing items API
   without changing its current consumers. Use validated filters and parameterized
   database queries. The existing search response does not expose the entire
   simulator contract, so the UI cannot simply reuse it unchanged.
3. Resolve real item IDs to their XML-selected assets and body variants.
   A model filename is not an item ID: item 11400350 uses model 11400151.
   Support item bundles containing multiple meshes/assets, not only one filename.
4. Extend the portable catalog/manifest contract with explicit identity,
   variant, slot, asset parts, attachment/customization metadata and availability.
   Keep detailed conversion failures in the QA report and concise availability
   explanations in the UI. Keep schema versions consistent across producer and
   consumer when the contract changes.
5. Extract a useful collection from our installed client archives into dedicated
   directories. Convert candidates per slot and body, retaining source provenance.
   Expand beyond the eight current variants. Unsupported candidates must not
   prevent usable candidates from being listed.
6. Build names, thumbnails, slot/gender filters, loading/error/empty states and
   an equipped-item summary. Show unavailable items as unavailable, or allow a
   supported-only filter. Do not silently substitute a different item.

Primary files: backend `NativeBatch.cs`, `ItemModelAttachment.cs`, archive and
diagnostic tools; frontend `nativeAssets.ts`, `/outfits/+page.svelte`, a focused
outfit API and catalog components. Inspect existing API/types before extending them.

Exit checks: find and equip supported items by name/ID in T3; correct gender and
model resolution; pagination/filter combinations; unavailable item handling;
no duplicate/ambiguous matching by basename. Report coverage by slot and body.

## Phase 2: Equipment rules and compatibility

Complete the rules needed to dress the character, not just isolated examples.

- Cover tops, bottoms, full outfits, gloves, shoes, hats, back items and supported
  weapon/accessory placements for both bodies. Support the core slot workflows
  even when individual items in those slots are unavailable.
- Implement full-outfit versus top/pants conflicts and source-driven body cutting.
  Do not generalize CL's prefix rule to every slot without inspecting the source.
- Preserve all required parts of a multi-part garment, including exposed skin.
  Restore replaced body parts when the item is removed or the body changes.
- Apply exact item-specific self/target attachments and source offsets. Handle
  multiple asset parts atomically so a failed part does not leave a half-equipped item.
- Distinguish canonical body joints from private equipment bones. Add private-bone
  support where needed for the selected library; mark unsupported animated pieces
  unavailable rather than freezing or pinning their bones silently.
- Verify body/gear compatibility beyond matching bone names: exposed seams,
  clipping, replacement coverage and motion. Different source shapes can share
  valid bone names and still fail as an outfit, as the staged event shirt did.

Primary files: `SkeletonGraft.cs`, attachment metadata, `sharedSkeleton.ts`,
`bodyVisibility.ts` and `OutfitScene.ts`.

Exit checks: complete outfits on both bodies, equip/replace/remove round trips,
full-outfit conflicts, correct skin inheritance, and representative motion.
Use numerical seam tests where two exposed meshes must meet, plus T3 views from
front, side and back. Do not resize body parts to conceal incompatible assets.

## Phase 3: Hair, faces and customization

1. Add hair and face selection through the catalog, including proper replacement
   of their constituent meshes. The current default face is not a face system.
2. Resolve hair/hat fitting, cutting and supported hair length/morph controls from
   the source XML/NIF and reference observations. `scale` and `capTransform` data
   have been found, but their application is not implemented.
3. Implement face texture/control sequences for blinking and selectable expressions.
   The recovered `emotion/item` imports and `emotion/common` frame sequences are
   inputs. Determine timing, loop and animation synchronization rules explicitly.
   Omit attached particles while retaining the face/material animation.
4. Connect game customization palettes/default selections and dyeability metadata.
   Preserve separate garment dyes and a shared body skin palette. Replace raw
   material names/numbered channels with understandable item controls.
5. Check alpha edges and transparent recoloring. The existing canvas path differs
   from the converter by up to three byte values on the sampled transparent face
   map. Establish acceptable visible behavior or use a lossless decode path.

Primary files: source customization/emotion export support, manifest schema,
`materialColors.ts`, `skinColors.ts`, outfit controls and runtime face animation.

Exit checks: switch faces/hair, fit a supported hat over hair, blink and select
expressions, dye/reset equipped parts, change bodies and re-equip after dyeing.
Face expressions must not require particle simulation to work.

## Phase 4: Backgrounds, poses and visual acceptance

- Add background selection using our own assets. Keep orbit, framing and image
  export usable with the selected background and character.
- Keep the six working player clips as the baseline. More poses are additions;
  a broken optional clip need not block release. Do not embed all ~2,000 clips.
- Define viewer root-motion behavior. If an in-place mode is offered, implement
  it as an explicit viewer choice without deleting source motion from exports.
- Establish consistent skin, fabric, hair, alpha and normal-map appearance.
  Verify back-face behavior where garments expose their reverse side. Do not
  assume validator success, copied light values or generic PBR proves fidelity.
- Reflections/special material behavior can block the affected items. Exact
  reproduction of every client shader is not a global simulator release gate.
- Check complete outfits across poses and camera angles, repeated item changes,
  load failures and ordinary browser viewport sizes. Check texture/resource cleanup
  and responsiveness with a real catalog rather than the small fixture list.

Exit checks: a recorded representative matrix for both bodies, every core slot,
hair/hat, face, dyes, one-piece conflicts and backgrounds; supported items render
consistently during playback; PNG export contains the selected appearance.

## Phase 5: Publish the supported library and simulator

1. Produce a reproducible versioned build of supported models, catalog and manifest.
   Separate converted-but-unverified assets from simulator-verified entries.
2. Validate the selected release exports and run producer/consumer contract checks.
   Missing and rejected asset counts remain separate from successes.
3. Exercise catalog loading and actual viewers against the proposed asset layout.
   Confirm URLs, MIME types, CORS and cache/version behavior before deployment.
4. Use native models for the verified simulator workflow. Keep Noesis explicitly
   available for other unresolved workflows. A global converter-default switch
   is not required to release this supported simulator library.
5. Prepare the concrete build, coverage report and release changes for review.
   Deploy/publish only with the required authorization, then verify the real flow.

Exit checks: users can browse supported clothing, assemble/customize a full outfit,
animate it, choose a background and export an image. Unavailable items fail clearly.
No release requires every NPC, map asset, effect or optional pose to convert.

## Reference website and evidence

The earlier Plan 08 research records behavior and bundle analysis of
`https://hunya.duckdns.org`. Treat those notes as historical observations, not
proof of the current site or of the client's implementation.

Recent fixes used our T3 local preview, client XML/NIF/HLSL and numerical
regressions. They were not established by a fresh live comparison with that site.
The source color formula also contains behavior beyond the earlier simplified
two-color reference-site description.

Use targeted live comparisons when they can resolve a concrete unknown:

| Unknown | Reference observation to collect | Source confirmation |
|---|---|---|
| Hair under a hat | Same supported hair/hat before and after equipping | Hair scale/morph, capTransform and cutting data |
| Full outfits | Which slots clear and which body parts remain | Itemmodel asset, slot and cutting definitions |
| Face expressions | Frame order, loop behavior and pose changes | Emotion imports, texture/control frames and delays |
| Dye defaults | Initial colors, available channels and reset | Customization palettes, dyeable flags and shader formula |
| Suspicious seams | Same item/body only if that combination exists there | Our source geometry, bind data and seam measurements |

Record the item IDs, body variant, selected state and observed result. A similar
looking item is not an exact comparison. If the site is unavailable or omits the
item, document that limit and continue independent work. Do not hotlink its assets
or ship its files. Reference observations inform behavior; our exports use our
own client sources and hosting.

## Optional follow-up, not a hidden release gate

Saved outfits/local restore would improve usability. Shareable preset links,
accounts, galleries and community submissions are not part of the current agreed
delivery. Define those requirements separately before implementing them.

## Deferred converter backlog

Balrog TCB/duplicate-sequence behavior, unrelated NPC/map failures, missing source
textures for isolated assets and complete reflection/billboard coverage remain
real work under Plan 09. They only move onto this plan's critical path when a
required simulator feature or selected release asset depends on them.

## Verification workflow

Use meaningful regressions for new decoding, attachment, state and timing rules.
Run the focused C#/Python suites after relevant backend changes and the outfit
frontend tests/typecheck after relevant frontend changes. Do not edit a checkout
while its tests are running. Use T3 for direct interaction and current visual
captures; preserve the user's running server and unrelated changes.

The existing unrelated nested-test failure in `unescapeHtml.test.ts` remains
documented. A focused pass must not be reported as a clean whole-project suite.
