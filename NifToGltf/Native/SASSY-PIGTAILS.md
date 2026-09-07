# Sassy Pigtails source fixes, 2026-09-07

Sassy Pigtails 10200010 is available in the separate, development-only preview
at `http://localhost:5173/outfits?hairPreview=sassy`. After explicit authorization,
both tail attachments were exported into `static/gltf/sassy-pigtails-preview-01/`
in the frontend. Release 14 supplies the existing A/C/D base hair and remains
unchanged, including its inventories and review hashes. The normal catalog still
marks this hair unavailable. Physics motion remains unimplemented: the preview
tails hold their rigid rest pose, without gravity, bending or sway.

## Explicit second-tail reference

The previous claim that the second tail source is missing was too strong.
The inspected Item archive has no P2_A NIF, but both P_A and P2_A KFM files
explicitly reference `00200010_F_PiPi_P_A.nif` and its corresponding KF.
They both name `Point01` as master and have identical bytes. This is an authored
shared model reference, not permission to substitute a similar hairstyle.

The local XML inspected is
`D:/Projetos/MapleStory2/XMLs/KMS2/Xml/itemmodel/102.xml`, SHA-256
`729e0effb06a8c86cd5035511dc172d303cd155bd1b48792ba3d97e1b063ac4b`.
This hash identifies this local file, separately from the earlier packaged XML
metadata source. Its two first placement positions are
`[55.85,-4.04907,-18.9015]` and `[54.6429,-4.64381,21.3994]`.

Existing extracted files under `obj/hair-investigation/source/0/02/`:

| File | SHA-256 |
|---|---|
| Both `00200010_f_pipi_p_a.kfm` and `00200010_f_pipi_p2_a.kfm` | `a3b0db2c52b63e08e138023eddd8e969d284cd3d5110cf707b99dfccd14a62c6` |
| `00200010_f_pipi_p_a.nif` | `2f10b2d4a9d8a0c43f0d30cbabd363c49089dbeccddf2e95b3f2273db807acdd` |
| `00200010_f_pipi_p_a.kf` | `272a2bf78aa623578782109e3298b72d708aef552446e2d060d2597326eeb906` |

`wardrobe_inventory.py` accepts an optional `--sources` extracted archive root.
Only unresolved URNs can follow an inspected, unambiguous KFM model reference.
Explicit existing NIF entries and ambiguities remain authoritative. The inventory
retains the KFM path, hash, model, master and clips. Preparation verifies that hash,
uses the referenced clips rather than a filename guess, and checkpoints their
hashes. Missing or invalid inputs remain blockers for that entry.

Native batch attachment selection uses the KFM identity while geometry uses its
declared NIF. The manifest's optional `attachmentSource` preserves that identity
for frontend placement. Old manifests continue to use `input`. This prevents both
tails from receiving the first tail's placement when their input mesh is shared.

## Constant missing animation target

The KF has exactly two transform evaluators, targeting `Point01` and
`Point01 NonAccum`. Both have posed channel bytes `[68,69,67,2]`, data reference
`-1`, zero translation and scale one. The second quaternion is WXYZ
`[1,0,0,-8.742277657347586e-08]`. Its sequence declares accumulation root
`Point01`. The NIF has that root and Bone01/Bone02/Bone03, but no NonAccum node.
There are no animated tail-bone tracks in this KF. The NIF contains PhysX data.

Claude Opus investigated the existing KMS2x64 program from PrivateMaple2 through
the restricted inspection bridge. The parent reviewed the returned decompilation:

- `0x141c6de00`, identified by its `NiMultiTargetPoseHandler::FillInfo` strings,
  initializes an entry to sentinel `0xfffe` and a null target. Failed named-target
  lookup logs a diagnostic and returns zero.
- `0x141c6d890`, `AddInterpControllerInfos`, only fills the binding priority and
  target setup when FillInfo succeeds.
- `0x141c6e7c0` searches existing children for the NonAccum suffix while setting
  up accumulation transforms. This inspected path does not create a missing node.
- `0x141c6edb0` guards channel writes with a signed priority comparison. The
  unbound sentinel is -2, so thresholds of zero or greater skip it. The caller
  `0x141c3cb40` supplies zero or a value from an associated object at +0x48.

The delegated report overstates this as an unconditional parity proof. Its own
remaining questions include possible negative thresholds, and the palette lookup
has an optional resolver that was not traced through Sassy's loading call site.
Those gaps remain open. The inspected binding code supports leaving unresolved
targets unbound; it does not justify dropping arbitrary missing animation targets
or claiming the entire client loading path has been reproduced. The 40 recorded
tool calls used inspection methods, with no mutation calls or permission denials.

The converter retains the declared accumulation root and posed-channel metadata.
It leaves a missing posed `<root> NonAccum` track unbound only when exactly one
declared root exists. Existing targets, duplicate names, animated missing targets
and unrelated missing names retain strict validation. It neither renames the
track nor creates a synthetic node. Skipped tracks are listed in
`unboundAnimationTargets` in the conversion report and native manifest.

This implements the inspected missing-target binding behavior in a narrow case.
It does not implement root-motion accumulation, PhysX playback or client parity.
The separate glTF export and static front/side appearance were verified below.

## Local preview verification

- 25 native C# tests pass, including direct reads of the real Sassy NIF/KF.
- Five KFM source tests and three existing inventory tests pass. The real P2 KFM
  resolves to the P NIF and KF, with its checked hash.
- The final full frontend run passed 144 tests with 15 skipped, including API
  guard and camera coverage. Svelte check reports zero errors and warnings.
  The preceding check completed before the recorded browser renders.
- Both tail glTFs pass validation with zero errors and warnings. Each has one
  mesh, 553 vertices, 813 triangles, three joints, six textures and one animation.
  All five input DDS files match the inspected local client archive byte for byte.
- The asynchronous `Diagnostics/render_sassy_preview.js` passes all 59 checks.
  All 18 front/side PNGs were visually inspected. Both tails, all independent
  placement presets, length 0.8/1.2, reset, C/D hat forms and hat removal were
  exercised. The saved dye fixture is EIIie's hair color, not a claim that EIIie
  wears Sassy. Nine checks compare the three hair pieces' baked texture pixels
  with the dye calculation across A/C/D forms.
- Equipping the Romantic Wedding Dress replaces both top and pants slots.
  Hair dye, placement and size survive hat-form replacement. Alternate forms
  retain each tail's explicit attachment instead of inheriting the base hair's.
- Side framing now uses depth as visible width and accounts for perspective
  depth. Projected-vertex checks confirm the full hair fits in all 18 captures.
  The existing 1.2 camera margin remains unchanged.
- All ten existing snapshot outfits were rerendered and all 20 captures inspected.
  Their final color controls and omissions match the previous report, preserving
  79 applicable saved dye checks and ten slot checks. Main PNGs and reports are
  updated, with previous files in `before-sassy-framing-9278caf/`.
- Release-14 native manifest and simulator catalog hashes remain respectively
  `37b1bfa2306665aa04419c0ba0f0b2fb7bfaa17d9cbaeb171ae139de73f2a135` and
  `962e5b3895aeb57c07d75fb9ea5d9ea0379216e306b39de35b9f29ccc8df63bc`.

Source evidence is in `obj/hair-investigation/sassy/source-report.json`.
Delegated queries are in PrivateMaple2's
`.scratch/handbook-hair-ghidra/evidence/sassy-binding-queries.json`.
The user requested local commits after this verification. No pushes or production
builds were made.

Export provenance is in the separate preview's `preview-provenance.json`.
Runtime checks and captures are in `obj/hair-investigation/sassy/renders/`.
Before-framing captures are preserved there under `before-framing/`.
The remaining appearance gap is substantial: rest-pose tails can point upward
or sideways and intersect hats. Client PhysX scene settings, solver stepping and
destination updates still need implementation and running-client comparison.
No approximate browser physics or game-client visual parity is claimed.
