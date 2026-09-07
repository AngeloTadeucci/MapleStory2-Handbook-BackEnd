# Hair follow-up, 2026-09-07

## Implemented locally

Saved appearance lengths now bypass the preset picker. `HR:0` and `HR0:0`
receive back length; `HR1:1` receives front length. Mesh traversal order and
whether a picker is visible no longer select the saved channel. Values are
retained through hat replacement. Reset still restores the exported morph.
EIIie's saved `[1, 1]` previously missed the adjustable back channel because
its picker only offered `[0.3, 0.5, 0.9]`.

Ponytail attachments now accept uniform size. Flower-knotted Hair exposes its
declared `0.8, 1, 1.2` choices. Both attachment pieces use the saved back length,
and position/rotation/size are reapplied after animation updates. This is
attachment scaling, not physics. The next client-UI audit enabled continuous
ranges for all supported channels, including the client reverse conversion
described below.

Client output inspected before the user requested no further Ghidra work:

- `0x14143e240`: applies the three exact morph names above.
- `0x141422340`: reads the stored float at hair extra data `+0x58 + 4*index`.
- `0x141167840`: forwards that value to the morph controller.
- `0x1416925e0`: updates the stored back length and both ponytail size records.
- `0x141695ca0`: includes the equipment scale in the local attachment transform.
- `0x14143f970` and `0x14143f3d0`: update equipment scale and scaled joint anchors.

Direct Ghidra work in the Codex session was paused at the user's request. The
user later authorized a separate Claude CLI investigation launched from
`PrivateMaple2`. See the [reviewed findings](HAIR-CLIENT-RESEARCH.md), including
corrections to Claude's initial report. Existing direct-inspection logs remain
under ignored `obj/hair-client/`.

## Source physics evidence

The user selected the client PhysX behavior rather than approximate browser
motion. Physics is **not implemented** by this change.

`Diagnostics/inspect_hair_physics.py` decodes props, actors, bodies, shape
descriptors, six-degree-of-freedom joints, and source/destination node links.
It requires complete payload consumption and validates decoded node/joint links.
The local NifSkope `nif.xml` supplies the layouts. Drive float parameters remain
raw because that old schema's labels need confirmation before solver use.

25 NIFs from `D:/MS2/KMS2 Debug/Data/Resource/Model/Item.m2d` were inspected.
Read-only extraction and reports are under `obj/hair-investigation/`:
`source/extraction-report.json`, `physics.json`, and `physics-summary.json`.
The report hashes each NIF. No release geometry or inventory was regenerated.

Curled Pigtails has four actors per tail. Bone01 supplies the animated source;
Bone02, Bone03 and Bone04 are physics destinations. UConstraint01/02/03 connect
that chain. The prop's PhysX-to-world scale is 100. Each joint locks translation
and twist and limits both swing axes. Authored swing limits are 15, 25 and 40
degrees. All six drive types are zero in the inspected tails. Body damping,
mass, anchors and local frames are retained in the report.

Flower-knotted Hair's original FlowerUp NIF has no PhysX blocks. XML jointangle
records alone do not provide the absent body/shape data. Do not invent those.

Remaining work for the client behavior:

1. Confirm the runtime meaning of XML `soft`, `posz`, `zero` and `negz` against
   the actual PhysX descriptors. The inspected helper writes joint limits and
   local frames; these should not be presented as an arbitrary spring or pose.
   The [Claude follow-up](HAIR-CLIENT-RESEARCH.md) establishes the attribute
   layout, four-record cap, soft default 0.01, and degrees-to-frame rotation.
   Exact solver semantics and units remain to be established.
2. Establish final scene settings and collision rules. The native probe below
   now reads the cooked hulls through the client's DLL; materials are decoded.
3. Establish the legacy PhysX stepping, scale conversion and destination update
   order, then validate against a running client capture. No browser solver or
   client-parity claim has been added.

## Ten hairs unavailable in the normal release catalog

These are the runtime failures after the existing 25 placement previews are
enabled. Missing names below were checked in the local archive extraction scope.
No guessed second tail or texture substitution was enabled.

| Hair | Remaining evidence |
|---|---|
| 10200010 Sassy Pigtails | Both KFM identities explicitly reference the existing P_A model. A separate authorized local preview now enables both tails at `/outfits?hairPreview=sassy`, with 59 runtime checks and 18 inspected captures. Release 14 still lacks tail geometry. See [Sassy details](SASSY-PIGTAILS.md). Physics remains absent. |
| 10200011 Banded Twin Tails | P2_A source absent. P_A contains physics. |
| 10200012 Cutesy Twin Tails | P2_A source absent. P_A has no physics blocks. |
| 10200031 Curled Pigtails | Both ponytail material records lack the required direction-texture binding. |
| 10200070 Curly Ponytail | KF expects absent Point01 NonAccum. Raw P_A has physics. |
| 10200159 Star Candy Hair | KFM references absent A.kf; raw A/C/D NIFs contain physics. |
| 10200246, 10200260, 10200262, 10200263 development hairs | Requested NIFs absent from the inspected archive scope. |

Curled Pigtails' raw tail NIFs name only D/S/N/C textures, while its base hair and
FlowerUp also bind `hair_a.dds`. Thus the missing exported direction binding is
also missing in the inspected raw tails. The client HLSL consumes Shader1 for
hair direction. The inspected material declaration has no named default texture.
This does not establish what fallback the running client uses. Resolve that in
the separate client session before enabling the hair.

## Verification

- Frontend: 131 tests pass, 15 skipped; Svelte check has zero errors and warnings.
- Physics decoder: three tests pass, including both real Curled Pigtails sources,
  joint-chain/limit assertions, scale 100, and truncated/trailing payload rejection.
- All ten snapshot outfits were rendered asynchronously in shared T3. All 20
  front/side captures were inspected. Omissions and final color controls exactly
  match the prior snapshot-verified render; 79 saved dye checks and ten slot
  checks pass. Sixteen nonapplicable dye checks remain null.
- Main PNGs, base64 companions, README and report are updated in
  `obj/live-outfits-20260907/`. Previous files are preserved in
  `before-saved-hair-lengths-9278caf/`. No ZIP was created.
- Flower-knotted Hair's size and placement controls were exercised visibly in
  T3, including animation seek. Equipping cap 11320024 changed hair form to `c`
  and preserved size 1.2, placement 2, and dyes. Physics motion remains absent.
- Release 14, private assets, production, and production builds are unchanged.

The previous alias task was committed as backend `3784001` and frontend
`9278caf`. The user subsequently requested local commits for these hair follow-up
changes. No push or deployment was requested.

## Continuous scale controls

The follow-up extracts client min/max/reverse and customize.scale from 255 XML
definitions. Supported non-reversed channels now offer sliders in the declared
range, using the verified client defaults 0 and 1 where bounds are omitted.
A single preset no longer hides an otherwise adjustable channel. Controls still
require an actual single morph target or a supported ponytail attachment.
The initial pass retained reversed presets; the follow-up below resolves them.
Saved floats and exported reset defaults remain independent of the picker range.

Frontend verification now passes 135 tests with 15 skipped; pnpm check reports
zero errors and warnings. Scale extraction adds three passing Python tests, and
the three physics decoder tests still pass. T3 checks exercised Lovely Rolling
Perm's two channels, including the single-preset front at 0.37 and back at 0.6,
reset to 0.5, and direct saved import at [1,1]. Flower-knotted Hair accepted 1.1
between presets, preserved its size and colors through a hat replacement and
removal, and was inspected from front and side at animation time 0.7.

EIIie and Asthoria were rerendered asynchronously from the existing 297-row
snapshot after pnpm check. All four front/side PNGs are byte-identical to their
current main-folder images and were visually inspected. Their 17 applicable dye
checks, both slot checks, saved hair values and omissions match the prior report.
The other eight main outfits were not rerendered in this UI-only follow-up.
Evidence is under ignored `obj/hair-investigation/ranges/`. Release-14 catalog
and native manifest hashes remain unchanged.

The [client research notes](HAIR-CLIENT-RESEARCH.md) document the remaining
PhysX/material gaps and an unreverted 220-byte Ghidra listing-definition change
made by the delegated investigation. No physics approximation was introduced.

## Reversed controls and native collision probe

The client beauty-shop input handler at `0x1418c78d0` and size-data builder at
`0x140f32a70` both apply `max - value + min` when reverse is nonzero. This is
an involution at the UI boundary. Saved values remain raw. The handbook now
uses that conversion for supported morph and attachment controls. Reset still
restores the exported value, including defaults outside the UI range.

Shared T3 checks exercised Anti-Gravity Braids 10200067 with bounds 0.4 to 1.5.
Displayed 1.1 produced raw 0.8; displayed 1.3 produced raw 0.6. Both front and
side renders were inspected. Reset restored raw 1 and displayed 0.9. Importing
saved `[0.8, 1.2]` retained those raw values. Evidence is under
`obj/hair-investigation/reverse/`. Frontend verification: 136 tests pass,
15 skipped; pnpm check has zero errors and warnings.

`Diagnostics/probe_physx284.cpp` initializes the local client's PhysX DLL through
its existing loader. It verifies that the SDK implementation belongs to the
specified core DLL. The runtime reports version 284, API revision 1. All 34
cooked convex hulls were read completely: 426 vertices and 716 triangles.
Curled Pigtails' two tails each contain hulls with 17/19/21/21 vertices and
30/34/38/38 triangles. No collision hull was substituted or recooked.

The probe also confirms the D6 descriptor field offsets inferred from Ghidra.
The five Python decoder tests and three scale-metadata tests pass. An additional
native integration test verifies both Curled tails, the descriptor layout and
truncated input rejection. See [the native probe instructions](PHYSX-PROBE.md).

This resolves the cooked-data and descriptor-layout gaps. It does not implement
browser physics. Final scene settings, scheduler order and a browser-compatible
legacy solver remain unresolved. Curled Pigtails' actual missing-map binding
also remains unconfirmed. No game-client visual parity is claimed.
