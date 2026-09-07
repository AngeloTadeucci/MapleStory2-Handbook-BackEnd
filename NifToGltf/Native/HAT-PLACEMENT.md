# Movable hat fitting, 2026-09-07

The local release-14 viewer corrects the two rigid cap assets reported at the
characters' feet: Blaze's Illusionist Feather Hat 11320361 and EIIie's Scarlet
Rosy Hairpin 11300858. The source CP mesh has an inverse-head local transform.
Keeping it cancels head height. Removing only its translation puts the hat across
the face, so that experimental correction was discarded.

## Client and source evidence

The existing KMS2x64 Ghidra archive was inspected read-only using
`Diagnostics/InspectHairClient.java` in the isolated HairPlacement project.

- `0x140546a90` reads `customize/capAttach` and `customize/capTransform`.
- `0x1405468d0` reads the cap transform's position and rotation without conversion.
- `0x1411e5c10` gets the equipped hairstyle's capTransform for movable caps,
  replaces the CP local transform, then projects onto the hairstyle's meshes.
  The default angles go directly to `0x142083d40`, so they are radians, unlike
  ponytail custom presets. Nonzero saved cap overrides are converted from degrees.
- `0x142083d40` constructs row-major `Rx(-x) * Ry(-y) * Rz(-z)`.
- The fitting ray follows transformed negative Z, starting 400 source units
  behind the cap position. Constants at `0x143a08cf8` and `0x142d1ed28` were read
  directly. `0x141424d00` tests the hair geometry; the nearest hit is selected.
- `0x140aacaa0` converts the hit world transform back through the parent inverse.

`Diagnostics/hat_placement_metadata.py` reads the same existing itemmodel/102.xml
as the hair placement extractor. All 255 records in the frontend's
`src/lib/outfits/hat-placement-source.json` were compared to that source.
Source SHA-256: `8db65b944ad4253bc716ee25ae60e52acdfefd2b24e41df9d772820fd4698dba`.

Blaze's hair 10200080 supplies position `[57.312,0.563721,30.6735]` and rotation
`[0.462381,-0.286844,0.049948]`. EIIie's hair 10200213 supplies position
`[50.2097,3.37549,20.2747]` and rotation `[0.415399,-0.753385,-0.333627]`.
Neither live cap appearance record contains saved placement overrides.

## Scope

The viewer replaces the rigid skin binding with the fitted head-local transform.
Its triangle tests use current skinned and morphed hair vertices. Hair changes
invalidate the fit, while head animation follows the shared skeleton without
repeating ray tests each frame. A missing hairstyle hides the movable hat and
shows an explicit warning until suitable hair is equipped.

The compatibility selector is restricted to the two diagnosed asset IDs inside
simulator-release-14. It does not alter fitted hats, other releases or private
character previews. General movable-cap dragging, saved cap overrides and other
capAttach assets are not enabled by this correction.

No release assets, review hashes, database records or production services changed.
The existing development frontend uses this code directly. The production build
has not been rebuilt for these local follow-ups.

## Verification

Frontend suite: 112 passed, 15 skipped. Svelte check: zero errors and warnings.
T3 front and side renders of both characters were inspected. Both hats retain
their bindings through animation; EIIie's fitting updates across hair lengths
0.3 and 0.9. Removing hair hides the hat with a warning, and re-equipping hair
restores it. Gelo's fitted beret was also rendered and inspected as a control.
Corrected PNGs are in `obj/live-outfits-20260907/corrected-hats/`; originals remain
in the parent directory. No new ZIP was created at the user's request.

Tests cover radian rotation, head-local attachment, following head animation,
nearest hair intersection, morph changes, repeated fitting, ray-miss fallback,
scope exclusions and unsupported skeleton rejection. These checks do not promote
the historical release appearance review.
