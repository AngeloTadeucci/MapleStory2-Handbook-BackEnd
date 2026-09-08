# Outfit studio continuation, 2026-09-08

Implementation is in the existing Svelte 5 `/outfits` route. Both handoff images
informed the two slot rails around the character and restrained dark framing.
The right panel combines equipment and outfits, with the selected item's colors
and customization. Full outfits occupy top and pants and are labelled in both.
Studio settings contain expression, background, skin and default eye colors.
Camera views, pose, playback, image export and text sharing remain accessible.

## Hair availability

`Diagnostics/hair-availability-audit.json` audits all 35 originally unavailable
release-14 hair entries. Twenty-five existing placement previews remain enabled.
Sassy, Banded and Cutesy now appear in the ordinary picker when their separate
preview exports are installed. Curly Ponytail adds one separately exported tail.
Release-14 files, manifest, catalog and historical review hashes are unchanged.

Sassy starts at the two reviewed source Position 3 controls. This is a viewer
initial choice, not modified XML or a new rotation. Position and size remain
editable and the source-reset action still selects Position 1. Its optional
browser motion is preserved. Banded, Cutesy and Curly remain static; the Sassy
three-bone solver is not applied to their different skeletons.

Curly Ponytail 10200070 uses its exact P_A KFM, NIF and declared KF. The existing
narrow converter rule leaves only the absent posed Point01 NonAccum translation,
rotation and scale channels unbound. A new real-source native regression checks
that all other tracks retain their root binding. The new glTF has 390 vertices,
517 triangles and four joints. Source physics is omitted. Its manifest, report
and hashed provenance are under `static/gltf/curly-ponytail-preview-01/`.

The local Item archive was reopened read-only. Six hairs remain unavailable:

| Item | Blocker |
| --- | --- |
| Curled Pigtails 10200031 | Both raw NiTexturingProperty blocks contain D/S/N/C at slot0/slot3/slot6/shader0, with no shader1 direction binding. No replacement texture was invented. |
| Star Candy Hair 10200159 | Each A/C/D KFM names a matching KF absent from this Item archive. The sequence name does not justify dropping the reference. |
| Development 10200246, 10200260, 10200262, 10200263 | Each has an explicit self itemPreset and itemmodel record, but its requested NIF is absent from this Item archive. Entries retain missing-source reasons in the catalog and audit. |

The absence findings apply to the local archive and extraction, not every client
version. The audit script records exact XML references and hashes. It does not
read player snapshots. No live database query, GameParser, production write or
asset publication was needed for the source investigation.

## Text format

`MS2O.` followed by unpadded URL-safe base64 of UTF-8 JSON, schema version 1.
There is no compression, dependency on compression streams, or executable data.
Encoding provides portability, not secrecy. The strict schema rejects unknown
properties, including player/account metadata. Private preview export is disabled.

Restores body, explicit catalog item IDs, hand and weapon placement, each material's
three saved dye channels, skin/default eye colors, both hair morph channels,
independent attachment positions and sizes, source makeup placement/size,
item animation selections, expression, background, body pose, play/pause,
hair effects and the optional browser-motion preference.

Camera angle/zoom, animation time, native PhysX diagnostic playback, search filters
and unequipped-item history are not stored. A fresh import frames the character
from the front and starts the selected pose at its beginning.

Limits: 48,000 input characters, 32,000 decoded bytes, 20 equipped entries,
32 material color controls per item, RGB in [0,1], hair lengths/sizes in [0,3],
up to four tails and 16 item animations. Actual preset choices, makeup scale,
body/face/pose compatibility, asset identities and animation names must also
match the current catalog and scene. No decompression occurs.

Imports resolve exact items and all occupied slots first. Duplicate identities,
conflicting garments, unsupported hands and incompatible hat forms fail before
loading a replacement. A separate scene then loads and validates all models,
colors and controls. Only success replaces the current scene. Load failures
and invalid customization discard the temporary scene and retain the old one.

## Verification

The final full frontend suite passes **171 tests**, with 15 existing skips.
`pnpm check` reports zero errors and warnings. All 34 native converter tests
pass. The new Curly Ponytail glTF validates with zero errors and warnings.
Five KFM-reference tests and five hair-physics decoder tests also pass.

Actual Chromium verification covers:

- All four newly accessible hairs, plus front/side/back captures. Sassy starts
  with source Position 3, accepts independent positions and dye, advances during
  continuous animation-frame playback, and freezes its simulation when paused.
- C/D hat swaps, hair replacement and hat removal. Distinct tail sizes 0.8/1.2
  and independent positions survive fitting. Banded, Cutesy and Curly remain
  static without incorrectly acquiring the Sassy solver.
- Full outfits occupy both clothing slots; replacing pants removes the full
  outfit. Both slot buttons show the linked full-outfit state.
- Export/import into a cleared outfit preserves logical appearance exactly in
  the tested cases. These cover skin, eye and item dye channels, both hair-length
  channels, independent tail sizes/positions, makeup placement/size, expression,
  background, play state and browser-motion preference. Two stowed copies of a
  weapon retain their distinct hands. Import also replaces a female body with
  the saved male body and compatible clothes.
- Malformed, truncated and oversized codes, unavailable/missing items, duplicate
  slot conflicts, invalid source placements, changed color-control counts and a
  forced model HTTP 404 all preserve the current outfit. The latter cases fail
  while preparing a separate scene, before the existing scene is replaced.
- Clipboard copy, text-file download, PNG download, keyboard slot activation,
  visible focus, mobile item selection and real mouse drag/scroll camera controls.
- Desktop and 390px mobile views have been inspected. Browser QA exposed and
  corrected resize clipping. Resize now retains orbit direction and relative
  zoom while fitting the changed aspect ratio. Studio backgrounds use centered
  cropping instead of stretching. Clicking a slot collapses customization so
  item choices appear first; its controls remain one disclosure away. The makeup
  dropdown reflects imported placement, and controls have stable accessible names.

The Chromium harnesses are `Diagnostics/outfit_studio_browser.mjs`,
`outfit_studio_extended.mjs` and `outfit_studio_final.mjs`. They require an
already-authorized server and never start one. Read-only HTTP requests are
allowed; application POSTs are blocked during QA. Only synthetic appearances
were used, with no player snapshot or account data in codes or public assets.

Reports and inspected captures are under `obj/outfit-studio-20260908/`:

- `chromium-02/`: 28 passing checks and all four newly accessible hair captures.
- `extended/`: six completed tail/load-failure checks before the harness stopped
  on the original implicit makeup label. The accessible labels were corrected.
- `extended-02/`: nine passing checks for the remaining full customization,
  hand/body restoration, image download and responsive framing scenarios.
- `final/`: seven completed compact-panel, clipboard/text, import and camera-drag
  checks before correcting the harness's mouse-wheel argument signature.
- `final-02/`: eight passing checks, including mouse-wheel zoom, proportional
  background cropping and the final desktop/mobile picker captures.

The first user-interrupted local run is not counted as completed QA. T3 status
and open calls reported no automation host; the user authorized Chromium instead.
No application script errors occurred in the completed scenario groups. These
checks and captures establish browser behavior, not game-client visual parity.
Hat/hair/shoulder intersections remain possible, the three non-Sassy new tails
remain static, and paired stowed stars still overlap at their source placement.

All 141 original handoff files match the supplied SHA-256 list. Release-14
manifest and catalog hashes still match the handoff. No production build,
deployment, production writes, commit or push was performed. The authorized
review server is bound to the tailnet at `http://100.118.72.53:4000/outfits` on oracle-machine.


## UI review corrections

The user requested the Handbook theme and removal of subtitle and diagnostic
clutter. The page and color controls now use shared surface, text and primary
colors. Panels use the existing `main-container` style. The plain character
background reads the Handbook `gray2` token, including after import or resetting
a scenic background.

The picker has 14 slots. Pendants and rings are entirely nonvisual in release 14.
Belts have 234 Empty.nif entries and ten unsupported entries. These three slots
and the explicitly requested Ears slot are removed. Earrings remain available.
The picker uses the existing available-item filter for both outfits and gear.
The subtitle, availability selector, Preview/Equipped/Verified badges,
Why unavailable details and selected-item diagnostic details are removed.
Selected borders, pressed state, removal buttons and full-outfit links remain.
The catalog and strict import validation still retain unavailable-item evidence.

`Diagnostics/outfit_studio_theme.mjs` passed 12 Chromium checks on the tailnet
server. It verified computed Handbook colors, removed elements, usable choices
for all retained slots and both bodies, color controls, full-outfit linking,
export/import after clearing, keyboard focus, mobile overflow and mobile equip.
No browser script errors occurred. Desktop and mobile captures in
`obj/outfit-studio-20260908/theme/` were inspected. The focused search and outfit
code tests passed 15 tests with one existing optional-asset skip. The updated
frontend typecheck reported zero errors and warnings.


## Original game slot artwork

The user's two additional screenshots and
`LithMS2-XML/Gfx/uimyinfodialog_i1.dds` now supply the empty equipment tiles and
checker-to-black character backdrop. Frontend `scripts/extract-outfit-ui.py`
extracts the uncompressed BGRA8 source without redrawing or recoloring it.
`static/outfits/source.json` records the source hash and exact crop rectangles.
No source DDS, existing release, or historical review file was changed.

Equipment surrounds one continuous canvas. Left: hats, eyewear, tops, pants,
back, right hand. Right: earrings, face accessories, gloves, shoes, left hand.
The removed pendant position stays empty. Equipped items use the Handbook's
existing rarity frame and outfit marker assets. Item rarity comes from existing
read-only label metadata; missing labels use a neutral frame. A full outfit
repeats a dimmed icon in the pants slot. Names remain in accessible labels and
hover/focus tooltips. Hair, Makeup and Face now use dedicated SVG controls centered along the bottom
inside the preview, following the user's later corrections.

The user asked how Hunya handles those appearance controls. A fresh Chromium
inspection of `https://hunya.duckdns.org/` found a separate group beneath the
left equipment slots: current hair thumbnail, lollipop with a makeup/blush
overlay, glasses with an eyes overlay, and a skin color swatch. Its base HTML
reuses CP/FA/EY/GL tiles, while overlays provide the visible appearance. Those
observations do not imply that we copied or hotlinked Hunya's assets.

`Diagnostics/outfit_studio_game_ui.mjs` passed 18 Chromium checks. It verified
all extracted tiles loaded, slot order, usable choices, rarity and outfit
markers, dimmed pants linkage, colors, an exact outfit-code round trip,
background restoration after import and scenery changes, keyboard focus,
mobile overflow and mobile equip. Desktop/mobile captures and front/side/back
views are in `obj/outfit-studio-20260908/game-ui-02/`. The previous `game-ui/`
run completed eleven checks before timing out during import. The separate rerun
traced both model loads, background loading and the scene swap to completion;
no application fix was made in response to that unconfirmed timeout.

The focused catalog/API/code/framing suites passed 23 tests with one existing
optional-asset skip. Typecheck reported zero errors and warnings. The review
server remains on the same tailnet URL. No commit or deployment was made.


## Appearance SVG controls

The user replaced the request for reused equipment placeholders with distinct
SVG icons, then requested that they be centered inside the preview. Frontend
`SlotIcon.svelte` now draws a hairstyle, a brush and compact, and a face with eyes.
These inline SVGs inherit the Handbook text color. The three 48px buttons sit
at the bottom center inside the canvas frame. They retain their icons after
an item is selected, with current item names in accessible labels and tooltips.

`Diagnostics/outfit_appearance_icons.mjs` passed 12 Chromium checks: distinct
SVG paths and theme color, desktop/mobile centering inside the preview, all
three item pickers, equip/remove, makeup placement, keyboard focus and mobile
overflow. No browser errors occurred. Empty and selected icon captures plus
desktop/mobile screenshots in `obj/outfit-studio-20260908/appearance-svg/` were
inspected. Typecheck reported zero errors and warnings.

## Named dye grid and custom color picker

The user's color-dialog references now inform the shared color UI. Basic colors
use a scrollable swatch grid with a selected checkmark, dye-name search, and
names on hover/focus. The existing Handbook `colorPalette` and
`getColorPaletteName` data supply all 220 named dyes and diagonal two-tone
swatches. Source basic palette colors remain available. Skin uses its authored
skin tones rather than the achievement hair/clothing dyes.

Custom mode has independent primary/accent/shade channel swatches, a draggable
hue/saturation plane, vertical brightness control and bounded 0-255 RGB inputs.
Arrow keys control the spectrum. The picker preserves all three logical color
channels. Achievement dyes author primary and shade only, so selecting one
preserves the current accent. Face controls use the dye's eye-specific values.
These changes use the Handbook theme, with no external color-picker dependency.

Implementation: frontend `ColorPanel.svelte`, `DyeControl.svelte`,
`CustomColorPicker.svelte` and `dyePicker.ts`. Five new unit tests verify named
dyes, two-tone previews, preserved accents, eye/skin selection, HSV conversion
across the RGB cube, hue wrapping and bounds. These and the outfit code suite
passed 16 tests. Typecheck reported zero errors and warnings.

`Diagnostics/outfit_dye_picker.mjs` passed 14 Chromium checks, including the
reference RGB pink, spectrum dragging and keyboard input, brightness, channel
independence, bounded RGB, reset, an exact cleared-outfit import/export round
trip and mobile selection. `outfit_dye_surfaces.mjs` passed six checks covering
clothing, eye-specific dyes and both palette/custom skin color. No browser
errors occurred. Desktop/mobile captures in `obj/outfit-studio-20260908/dye-picker/`
and `dye-surfaces/` were inspected. Earlier browser captures and harnesses record
the previous dropdown-based color UI; these new harnesses cover its replacement.

## Color update performance and unused channels

The user reported an apparently ineffective Accent on Shiny Long Velvet Hair
10200124 and lag while changing colors. Chromium inspection confirmed that
Accent changes only a small part of that hair's texture: toggling black/white
changed 1,119 RGB byte components, compared with 668,697 for Primary and 771,288
for Shade. This is texture evidence, not a measurement of visible mesh coverage.
Accent remains enabled for Shiny and Sassy Pigtails. Curly Ponytail has no Accent
contribution in its color masks, so its Custom mode disables that channel and
explains that the item does not use it. Shared hair controls combine usage from
all attachments. Saved logical colors still retain all three channels.

Frontend `materialColors.ts` now prepares texture samples once and reuses the
output pixels, canvas and ImageData. Repeated changes blend the cached samples;
identical colors and changes confined to unused channels skip texture uploads.
`faceAnimation.ts` also reuses prepared samples and texture buffers for every
expression frame. The original `bakeColors` remains the numerical reference.

`Diagnostics/outfit_color_performance.mjs` measured these CPU update times in
Chromium on the review machine, using SwiftShader:

| Hair | Before | After |
| --- | --- | --- |
| Curly Ponytail | about 1,284 ms | about 28 ms |
| Sassy Pigtails | about 955 ms | about 21 ms |
| Shiny Long Velvet Hair | about 596 ms reference blending alone | about 16 ms complete color control update |

These are local CPU timings, not end-to-end frame rates or guarantees for other
PCs. Before/after evidence is in `obj/outfit-studio-20260908/color-performance-before/`
and `color-performance-after/`. Curly and Sassy produced identical channel-change
counts before and after optimization.

The material, shared hair, skin, dye picker and outfit code suites passed 28
unit tests. New tests compare exact pixel bytes across sampling modes, source
sizes, alpha and colors; check reusable output and channel detection; and keep
Accent enabled when any hair attachment uses it. Typecheck reported zero errors
and warnings. Both repository diffs passed whitespace checks.

`Diagnostics/outfit_color_regression.mjs` passed 15 Chromium checks on Shiny,
including pointer dragging, keyboard input, bounded RGB, independent Accent,
reset, exact export/import and mobile selection, plus the disabled Curly Accent.
The dye surfaces harness passed six checks for clothing, face dyes and skin.
No browser errors occurred. Desktop/mobile captures in `color-regression/` and
`color-surfaces/` were inspected. One initial regression attempt stopped on an
ambiguous test searchbox selector; the corrected full rerun passed.

The authorized review server was restarted after its earlier session ended and
remains available at `http://100.118.72.53:4000/outfits`. No commit or deployment
was made.

## Appearance slots at the top

The user requested the appearance slots at the top, matching equipment slot
sizes and showing the selected hair thumbnail. Hair, Makeup and Face now sit
at the top center inside the preview. They inherit the equipment dimensions,
65px on desktop and 48px on mobile. Equipped appearance slots display the item
thumbnail and shared item frame; empty slots retain the custom SVG drawings.
Their tooltips open below the slots to stay inside the preview.

`Diagnostics/outfit_appearance_top.mjs` passed 15 Chromium checks covering top
centering, matching desktop/mobile dimensions, all three thumbnails, picker
navigation, makeup customization, removal, keyboard interaction, tooltip
placement and mobile overflow. No browser errors occurred. Desktop/mobile
captures in `obj/outfit-studio-20260908/appearance-top/` were inspected. Svelte
check passed with zero errors and warnings before the final tooltip-only CSS
adjustment, which was included in the full browser rerun.

The preceding thumbnail cleanup also removed the background slot drawing from
catalog item cards. The Techwear Scout Mask (F) image loaded alone in Chromium;
the equipment rails retained their slot artwork.

## Appearance icon styling

The user requested that the empty appearance icons resemble the equipment
artwork. `SlotIcon.svelte` now supports a framed variant with a beveled gray rim,
inset dark well, and subdued filled SVG symbols with an embossed edge. Only the
empty appearance slots use that variant. Their symbols grew from a 34px drawing
box to about 42px on desktop, with the existing 65px slot dimensions unchanged.
The white outlines and separate rounded button surface were removed from these
slots. Selected item thumbnails still replace the empty drawings.

`Diagnostics/outfit_appearance_framed.mjs` passed 15 Chromium checks, including
frame styling, desktop/mobile size and placement, thumbnail loading for all
three slots, equip/remove, keyboard selection, customization and tooltip
placement. No browser errors occurred. The empty preview capture was inspected
alongside the original equipment tiles. Evidence is in
`obj/outfit-studio-20260908/appearance-framed/`. Typecheck reported zero errors
and warnings, and frontend diff whitespace checks passed.

## Appearance slots reuse equipment tiles

The user replaced the custom appearance drawings with the exact original tiles
indicated by screenshot arrows: Hair uses the helmet, Makeup the lollipop and
Face the glasses. `slotArtwork.ts` maps HR/FD/FA to those existing atlas extracts;
`SlotIcon.svelte` now renders the mapped image without the custom SVG variant.
Equipped thumbnails, top centering, slot dimensions and accessible names remain.

`Diagnostics/outfit_appearance_atlas.mjs` passed 14 Chromium checks covering the
three exact image paths and loaded images, desktop/mobile dimensions and
placement, equipped thumbnails, removal restoring the lollipop, customization,
keyboard input, tooltip placement and mobile overflow. No browser errors occurred.
The empty preview screenshot in `obj/outfit-studio-20260908/appearance-atlas/`
was inspected. Typecheck and frontend whitespace checks passed.

## Removal, makeup replacement and direct movement

The user reported that equipping another makeup item did not update the face,
and requested clear removal and better positioning. The initial instrumented
Chromium run reproduced the bug: after replacing Heart Tattoo with Star Tattoo,
the control and loaded texture belonged to Star, while FA_Skin's compiled shader
still referenced Heart's texture and transform. Position changes on the newly
selected item therefore never reached the rendered face.

`faceDecal.ts` now maintains shared uniform objects per skin material and repoints
them when a new decal attaches. This fixes reuse of Three's cached shader program
without generating a new program per makeup swap. A regression test compiles the
first decal, replaces it without recompiling, and verifies that the cached shader
uses the new texture and movement values. Browser verification repeats Heart,
Star and Heart swaps and checks the actual renderer uniforms.

Every equipped slot has a visible X button. The right panel also labels its
removal action as Remove. These actions reuse existing bundle removal, including
linked equipment slots. Hair, Makeup and Face retain the requested original
helmet, lollipop and glasses artwork when empty.

Move on face pauses the pose, opens a front view and enables direct mouse/touch
dragging over the face mesh. Picking uses the animated skin's UV coordinates and
refreshes its picking bounds. Arrow keys provide small adjustments. Rotation is
shown in whole degrees and size retains the source item's range. Translation and
rotation are enabled only by the corresponding source customization flags; fixed
blush has neither control. The old position dropdown is replaced with preset
buttons, and position controls precede the dye grid.

Movable hair attachments have separate Move buttons, continuous position sliders,
preset buttons and reset. Dragging horizontally interpolates translation and
quaternion rotation between the authored points; it does not introduce arbitrary
3D attachment coordinates or change the underlying presets. Twin tails remain
independent. Existing length and size controls remain available.

Outfit codes retain continuous hair positions and optional bounded makeup offsets
and rotation. Existing codes without these optional fields still import. Runtime
restoration enforces each item's movement capabilities and scale range. Encoding
sorts items by ID and hand so equip order does not change the resulting code. The
browser round-trip check initially exposed an ordering-only mismatch; the saved
positions and colors were already identical. A unit test now covers deterministic
encoding without mutation of the input.

Verification: 27 tests passed across face decals, hair placement, saved hair
lengths and outfit codes; Svelte check reported zero errors and warnings. The
final `Diagnostics/outfit_movement.mjs` Chromium run passed 16 checks covering
repeated shader replacement, presets, direct dragging, keyboard adjustments,
rotation/size, independent fractional hair movement, exact export/import, rejected
invalid values preserving the outfit, mobile slot removal and touch dragging,
and fixed makeup capability gating. No browser errors occurred. Desktop/mobile
captures in `obj/outfit-studio-20260908/movement/` were inspected. Earlier placement
harnesses target the former dropdown UI; this harness covers its replacement.
The initial makeup investigation is retained in `makeup-debug/`. No source assets,
production deployment, commits or external application writes were made.

## Washed-out makeup under ambient lighting

The user reported that Heart Tattoo looked transparent. Inspection of the
unchanged source RGBA texture found a dominant interior alpha of 238/255, about
93%, rather than the near-invisible result. The decal hook blended only into
`diffuseColor`, while the character shader subsequently captured the original
skin texel for its separate ambient calculation. Ambient lighting therefore
ignored makeup entirely.

`faceDecal.ts` now composites the original decal alpha into `sampledDiffuseColor`
before the character shader captures the ambient texel. Direct lighting uses the
same composite multiplied by the material's diffuse coefficient. The shader cache
key was advanced for the changed shader body. No texture, alpha value, dye palette
or studio light intensity was altered.

`Diagnostics/outfit_makeup_lighting.mjs` reproduced the bug in Chromium with
controlled lights and black/white makeup dyes. Before the fix, ambient-only
lighting changed zero pixels. Afterward it changed 133 pixels with a maximum
channel difference of 181/255. Direct-only and normal studio light also passed.
The normal studio before/after captures were inspected: the faint skin-colored
heart is now visibly red. Evidence is in `makeup-lighting-before/` and
`makeup-lighting-after/` under `obj/outfit-studio-20260908/`.

Seventeen focused decal, character-lighting and color-mask tests passed. A new
integration test checks that decal composition runs before ambient texel capture
and retains the original alpha. Svelte check reported zero errors and warnings.
The movement harness passed all 16 checks again, including makeup replacement,
presets, rotation/size, dragging, mobile touch/removal and exact import/export.
Its new evidence is in `makeup-lighting-regression/`; no browser errors occurred.
The review server stopped during verification and was restarted at the existing
authorized tailnet address. No commit, deployment or source asset edits were made.
