# Authored hair placement, 2026-09-07

See [the follow-up](HAIR-FOLLOWUP.md) for saved length corrections, attachment
size controls, and the source physics investigation. The verification below
describes the initial placement change.

The local frontend now applies the source custom position and rotation presets to
each ponytail attachment. It reapplies them after equipment animation updates,
keeps independent controls for twin tails, remembers choices through hat-form
replacement and resets to the first source preset. Base hair and ponytails share
one dye control. Release-14 files and review hashes are unchanged.

## Source and client evidence

`Diagnostics/hair_placement_metadata.py` extracts metadata from the existing
Oracle `NifToGltf/obj/wardrobe/xml/itemmodel/102.xml`. The frontend stores it in
`src/lib/outfits/hair-placement-source.json`. Source SHA-256:
`8db65b944ad4253bc716ee25ae60e52acdfefd2b24e41df9d772820fd4698dba`.

The local KMS2x64 Ghidra archive was imported into an isolated ignored project at
`NifToGltf/obj/hair-client/HairPlacement`. `Diagnostics/InspectHairClient.java`
decompiles functions and lists cross-references without editing the program.

- `0x142083d40`: Matrix3 FromEulerAnglesXYZ. The named Lua wrapper at
  `0x14222f140` confirms the function. It constructs row-major
  `Rx(-x) * Ry(-y) * Rz(-z)`.
- `0x140a33480`: standard row-major matrix multiplication used by that routine.
- `0x141695ca0`: item rotation degrees converted to radians before that call.
- `0x141693210`: ponytail controller calls the same Euler routine.
- `0x1416962d0`: CPonyTailPhysXHelper::UpdateModelPhysXValues, identified by its
  embedded error string. Matches joint names and applies jointangle data to
  joint limits and local frames. These are physics settings, not interpolated
  skeleton poses.

Flower-knotted Hair 10200008 attaches Point01 to Bip01 Head. Its exported empty
sequence overwrites Point01 transforms, so applying a preset only on equip is
insufficient. Its original NIF has no PhysX blocks or UConstraint nodes.

## Coverage and limits

25 placement-blocked hairstyle entries now load using existing release geometry.
Curled Pigtails 10200031 remains unavailable: its exported ponytail materials
lack the required hair-direction texture. No texture was invented or regenerated.
Physics motion is not implemented. Source jointangle records are retained, not
misrepresented as static placement controls. These entries remain previews;
this change does not promote historical appearance-review evidence.

T3 runtime sweep loaded the 25 hairs across male and female bodies, selected their
last authored presets, sought animation and reset. Curled Pigtails exposed the
texture failure above. This is runtime coverage, not visual approval of all 25.
Frontend tests: 107 pass, 15 skipped. Svelte check: zero errors and warnings.
Unit regressions cover Euler signs/order, animation overwrite, attachment
isolation, shared dye/reset and the 25-entry availability change.
Production build has not been rebuilt for this change.
