# Clothing simulator completion plan

Updated 2026-09-06 following the user's revised acceptance scope.

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
