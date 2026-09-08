# Outfit simulator continuation on oracle-machine

Use this file as the next-session task prompt. Implement the work below in the
existing Handbook projects, rather than producing only a proposal.

## Workspace and preparation

- Backend: `/home/ubuntu/repos/MapleStory2-Handbook-BackEnd`.
- Frontend: `/home/ubuntu/repos/MapleStory2-Handbook`.
- Both branches: `feat/clothing-simulator`. Read both repositories' AGENTS.md and
  applicable guidance. Inspect status, fetch and compare upstream before edits.
  Preserve local work. Do not reset, auto-stash, or switch branches to discard it.
- Use pnpm and Linux `python3`. Existing pnpm is under
  `/home/ubuntu/.local/share/pnpm`; add that directory to the shell PATH if needed.
- Oracle has .NET 9/10 runtimes but no .NET 8 runtime. The converter targets
  net8.0 and compiles with the installed SDK. Use command-scoped
  `DOTNET_ROLL_FORWARD=Major dotnet run --project NifToGltf.Tests` for its tests
  and the same prefix when invoking the converter. This was verified with 33
  native tests passing. Do not install a runtime or change machine configuration
  just to reproduce this checked setup.
- Read STATUS.md, HAIR-PLACEMENT.md, HAIR-FOLLOWUP.md, SASSY-PIGTAILS.md,
  BROWSER-HAIR.md, HAIR-MOTION.md, HAT-PLACEMENT.md and ASTHORIA-DRESS.md here.
- UI references are `ui-reference-1.png` and `ui-reference-2.png` beside this file.
  Inspect both images. They illustrate the desired equipment-slot arrangement
  and visual character, not a request to duplicate every game-client decoration.

## 1. Finish missing hair previews

Audit current catalog and actual source/export failures, beginning with:

- Curled Pigtails 10200031: both raw tail material records lack the required
  hair-direction binding. D/S/N/C exist; an arbitrary hair_a texture substitution
  has not been justified. Investigate the actual material path and available
  source data. Keep any explicit browser approximation distinguishable from a
  recovered client binding. Do not silently invent a replacement texture.
- Curly Ponytail 10200070: missing Point01 NonAccum animation target. Reassess
  using the narrow constant-unbound-target handling already implemented for
  Sassy. Do not broadly ignore missing animated targets.
- Star Candy Hair 10200159: KFM references an A.kf absent in the earlier inspected
  scope. Recheck exact references and archive contents.
- Development hairs 10200246, 10200260, 10200262 and 10200263: requested NIFs
  absent in the prior search scope. Confirm whether they are real usable catalog
  entries and report genuine missing sources explicitly.
- Audit other currently unavailable hair entries instead of assuming this list
  remains exhaustive. Record each blocker as source, alias, animation target,
  material, placement, or omitted feature. Use explicit XML itemPreset and KFM
  references, preserving valid explicit catalog entries. Never guess replacements.

Sassy 10200010, Banded 10200011 and Cutesy 10200012 are now separate local previews
at `/outfits?hairPreview=twins`. Banded and Cutesy were explicitly confirmed by
the user. Both missing second-tail KFMs name their first-tail NIF. Banded has four
skin bones and omitted source physics; Cutesy has one skin bone and no PhysX
blocks. They currently remain static. Do not blindly apply Sassy's three-bone
solver to them. Finish availability and user access as part of the work, with
honest preview limitations, rather than leaving successful fixes discoverable
only through an undocumented query string.

The user accepts an approximate browser simulation and said Sassy motion looks
okay. Exact PhysX reverse engineering is no longer a prerequisite. Preserve that
working option. Their placement complaint meant attachments should be farther
back, not above the ears. Both source Position 3 controls gave a lower rear
attachment in the reviewed viewer. That selection is not yet a persisted default.
Do not alter source presets or introduce arbitrary rotations on that basis.
Current collision model can intersect head hair, hats and shoulders. No client
visual parity is claimed. Exact client research is documented for optional later
work; Windows PhysX DLLs cannot execute directly on Oracle ARM/Linux or browsers.

Sources already on Oracle include backend `NifToGltf/obj/wardrobe/`, its XML and
archive indexes, and frontend release 14. The setup also transfers the small
`NifToGltf/obj/hair-investigation/source/` extraction and selected source evidence,
plus `static/gltf/sassy-pigtails-preview-01/` and `twin-tails-preview-01/`.
Inspect current manifests/provenance before conversion. If a new export is needed
for a verified missing input, put it in a separate preview directory. Preserve
existing release files, inventories, review hashes and private Gelo assets.

## 2. Rework the full outfit UI

Build this in the existing Svelte 5 `/outfits` route and project design system.
The user wants a polished, usable character dressing screen based on the two
reference images:

- Keep a large, well-framed character preview central, with clickable equipped
  slot icons arranged around it. Show the current item or a clear empty state.
- Clicking a slot opens its relevant item choices on the right. Put search,
  filtering, equip/remove actions, colors and that item's customization in a
  coherent selection panel. Make the active slot and equipped choice obvious.
- One combined equipment/outfit experience. Do not add Outfit versus Gear tabs
  or separate wardrobes. Items of both types belong in the same slot picker.
- Full outfits replace both top and pants correctly, with an understandable
  indication across both slots. Preserve hand placement and other existing slot
  rules. Selecting and replacing hair must preserve compatible hat forms.
- Keep body, pose/play/pause, front/side/back, image export, expression and
  background controls easy to find without crowding the clothing picker.
- Hair position/length, palette/custom dye, effects and relevant controls should
  be accessible for the selected item. Use clear labels and distinguish preview
  limitations from unavailable items. Keep diagnostic experiments out of the
  primary user flow. Do not expose technical identifiers as the main UI language.
- Use readable spacing, restrained game-inspired framing, consistent icons,
  helpful empty/loading/error states, keyboard navigation and visible focus.
  Adapt the right panel to a usable mobile layout. Preserve camera interaction.

Inspect the existing route and scene APIs before factoring components. Reuse
working material, slot replacement, customization and attachment logic rather
than duplicating it inside the new UI. Propose materially different scope before
implementing it; routine component and visual decisions are yours.

## 3. Outfit import/export

Add a shareable text code with copy/export and paste/import. Prefer a versioned
JSON schema encoded as URL-safe base64, optionally using browser-supported
compression if it materially reduces real outfit codes. Encoding is not secrecy.
Choose the simplest format with reliable cross-browser decoding and describe it.

Persist logical outfit state, not glTF files or transient scene objects: body,
explicit item IDs and hand selection, per-item saved dye channels, hair placement
and length, and relevant expression/background/customization settings. Preserve
the optional browser-motion preference if applicable. Define exactly what the
format restores and avoid silently resetting colors or independent tail choices.

Validate schema version, numeric bounds, duplicate/conflicting slots, item/body
compatibility and input/decompressed size limits. Treat codes as untrusted data,
never executable content. Resolve through the existing catalog, with clear
missing/unavailable-item feedback. Validate before replacing the current outfit;
avoid leaving users with a partially destroyed outfit on a failed import. A
successful full-outfit import must honor top/pants replacement. Do not add a
database, account requirement, backend writes or cloud persistence for this.

## Verification and operating limits

- Production lith is read-only. No DB writes, migrations or GameParser. No fresh
  live query is needed; use backend
  `NifToGltf/obj/live-outfits-20260907/corrected.zlib.base64`, a zlib-compressed,
  base64-encoded NDJSON snapshot. EIIie uses two capital I characters.
- Never expose snapshot player/account data in tracked files, public assets or
  exported outfit codes. Import/export needs appearance data only.
- Do not start review/dev servers or kill existing processes automatically.
  Oracle's old review servers were intentionally stopped. Use an existing
  authorized browser/server if available; if none exists, make implementation
  and checks ready, then ask specifically to start a review server for visual QA.
- Do not modify .env files, old releases, simulator-release-14, review hashes or
  private Gelo assets. Do not deploy production or publish assets as part of this
  task. No commits or pushes for the new work unless the user asks again.
- Follow host-specific SSH rules and display exact remote commands before use.
- Run meaningful solver/import-export/slot tests and `pnpm check`. Finish check
  before browser harness injection because Svelte sync can reload and erase state.
  Do not edit a checkout while tests run against it. Long render jobs run
  asynchronously and are polled.
- Current Windows baseline: 155 frontend tests passed, 15 skipped, typecheck
  clean. Browser harnesses passed 27 Sassy and 53 twin-tail checks; 32 front/side
  captures were inspected. T3 continuous animation callbacks stopped during the
  automated final check, so continuous playback/pause was not verified there.
  The user subsequently reported motion looks okay. Keep those evidence scopes
  distinct. Production builds have not been rebuilt for these changes.
- Verify actual front/side/back appearance, animated motion, saved dye, independent
  hair controls, hat swaps and complete-outfit replacement in a real browser.
  Verify desktop/mobile UI, keyboard flow, and an export/import round trip into
  a cleared outfit. Test malformed/truncated/oversized codes and missing items.
- Existing live-outfit report/images are under the snapshot directory. If new
  work requires refreshing them, preserve originals in a backup subfolder and
  update PNGs directly in the main folder plus report.json and README.md. No ZIPs.

Report implemented results, runtime evidence, remaining unavailable hairs and
specific visual limitations. Do not claim game-client parity from tests alone.

## Setup verification on Oracle

Both checkouts were fast-forwarded after the local hair commits were pushed.
The copied preview/source/evidence/snapshot set contains 141 files, all verified
against local SHA-256 hashes. The checksum list is at backend
`NifToGltf/obj/oracle-handoff-hashes.sha256`, relative to `/home/ubuntu/repos`.
Oracle frontend tests passed 155 with 15 skipped, and `pnpm check` reported zero
errors/warnings. Native converter tests passed 33 using the roll-forward above;
five KFM-reference tests also passed. Release-14 manifest/catalog hashes match
the recorded Windows baseline. No Handbook servers were started. An existing
GenGame preview uses port 4173; do not stop it or assume it is the Handbook.
