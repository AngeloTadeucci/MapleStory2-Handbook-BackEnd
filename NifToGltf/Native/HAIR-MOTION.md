# Local Sassy PhysX motion sample

The user subsequently authorized a separate [browser approximation](BROWSER-HAIR.md)
for interactive gravity and sway. This document describes the earlier native
sample and its unresolved client-parity work, not the new browser solver.

The first motion sample uses the user's existing PhysXCore64.dll 2.8.4.6.
It is offline solver output replayed in the development-only Sassy preview,
not a browser physics solver or a reproduction of the complete client behavior.
The UI labels its experimental settings and exposes an eight-second sample.
The earlier static hair work was committed as backend `2423c12` and frontend
`b4475b4`. The user subsequently authorized committing and pushing the motion
work together with the browser approximation and twin-tail previews.

## Inputs and execution

`prepare_hair_motion.py` reads the actual Sassy NIF through the existing decoder.
It writes a C++ include for three actors, three original cooked hulls, a material,
and two D6 joints. Bone01 is kinematic. Bone02 and Bone03 are dynamic destinations.
Both tail identities explicitly share this NIF through their KFM records.
No collision geometry is recooked and no rendering asset is regenerated.

Sassy's inspected local itemmodel XML has no jointangle records. Its NIF joint
frames, limits and drives are retained. The adapter gives authored body mass
priority and sets actor density to zero to satisfy PhysX descriptor validity.
That is an SDK mass-mode requirement, not a verified trace of the client's actor
creation code. All actor, shape, material and joint descriptors pass SDK validity
checks, and native creation/stepping produced no SDK diagnostics.

`capture_sassy_motion.js` samples fitting_idle_a at 60 Hz in the existing T3
viewer. It captures both Bone01 world transforms and all six initial bone poses.
`bake_hair_motion.py` aligns each source actor chain to those exported rest poses,
runs `simulate_hair284.cpp`, and records output poses and provenance.
Rest-transform maximum error is 4.60e-8; root-position maximum error is 1.79e-7
metres. Both destination chains move across the 481 output frames.

The experiment uses a fixed timestep of 1/60 second, one substep, gravity
`[0,-9.8,0]` in glTF Y-up metres, and SDK default scene filters. Only the six
authored hair collision shapes are present. Character and hat shapes are absent.
These choices are recorded experimental inputs, not established client settings.
The browser retains default positions on both tails, size 1, and the fitting idle
body animation. Other placements, sizes and hats require a new simulation.

## Reproduction

From the backend root, prepare the separate ignored scene directory:

```powershell
py NifToGltf/Diagnostics/prepare_hair_motion.py NifToGltf/obj/hair-investigation/source/0/02/00200010_f_pipi_p_a.nif NifToGltf/obj/hair-investigation/sassy/motion
```

Using the existing x64 MSVC environment and SDK reference headers described in
[PHYSX-PROBE.md](PHYSX-PROBE.md), compile from `obj/hair-investigation`:

```powershell
cl /nologo /std:c++17 /EHsc /MD /DWIN32 /DWIN64 /DNX64 /Iphysx-sdk-reference/SDKs/Physics/include /Iphysx-sdk-reference/SDKs/Foundation/include /Iphysx-sdk-reference/SDKs/PhysXLoader/include /Isassy/motion ../../Diagnostics/simulate_hair284.cpp /Fe:sassy/motion/simulate_hair284.exe /Fo:sassy/motion/simulate_hair284.obj
```

Finish `pnpm check` before evaluating the capture harness in the existing
`/outfits?hairPreview=sassy` T3 tab. Run asynchronously and poll
`window.sassyMotionInput.running`. Save its JSON as `motion/capture.json`.
Then, from the backend root:

```powershell
py NifToGltf/Diagnostics/bake_hair_motion.py NifToGltf/obj/hair-investigation/sassy/motion/capture.json NifToGltf/obj/hair-investigation/sassy/motion 'D:/MS2/KMS2 Debug/x64' ../MapleStory2-Handbook/static/gltf/sassy-pigtails-preview-01/motion-fitting-idle.json
py -m unittest discover -s NifToGltf/Diagnostics -p test_hair_motion.py
```

The binary input is bounded and rejects truncation, nonfinite values, invalid
rigid matrices and trailing bytes. The bake validates frame counts, normalized
rotations, source/export rest alignment and the driven roots' trajectories.
DLLs, SDK headers, generated include, hulls, executable and motion JSON remain
local ignored artifacts. Release 14, inventories and review hashes are untouched.

## Verification

Three native-motion tests pass, including actual client-DLL stepping, independent
tail bending relative to the driven root, coordinate alignment and rejection of
truncated input and determinant-one shear. The final frontend motion, Sassy,
placement and saved-length run passes 16 tests. Svelte check has zero errors and
warnings and completed before rendering.

`verify_sassy_motion.js` passes all 49 browser checks. Both destination bones on
both tails match native output at 0, 1, 3, 5 and 8 seconds. World-position error
stays below 1e-6 metres and normalized rotation error below 1e-5 radians. The
angular assertion normalizes decomposed quaternions before comparing angles.
All ten front/side captures were visually inspected. EIIie's saved pink dye is
used only as a color regression fixture and remains unchanged through playback.
Rest transforms restore exactly, unsupported placement is rejected, and changing
placement during playback cancels the sample and restores the bones.

The actual UI button completed its full eight-second run and restored the rest
pose. Recorded evidence is in `obj/hair-investigation/sassy/motion/review/`.
Maximum tip rotation relative to the root is about 11.94 degrees in this test.
This confirms actual joint motion rather than movement from the body alone.
The new input-validation build reproduces all 481 previously inspected frames
exactly. The native executable, input and client DLL hashes are in the motion JSON.

## Remaining client behavior

The user confirmed that the tails still point upward during this sample. The
solver and playback checks above do not validate the starting orientation. The
kinematic root preserves that starting pose and the authored D6 swing limits
allow only small bends. More playback time does not address this appearance gap.

Cached client code contains an additional attachment-orientation path that the
preview does not implement. In `0x141694650`, `0x141202480` supplies the initial
position, rotation and direction, and `0x141695bc0` participates in the second
tail's position/direction calculation. `0x141693210` builds a new rotation from
its direction input and sends it to equipment virtual slot `+0xf0` before calling
the PhysX joint helper. The subsequent KMS review below establishes that this body
requires the animation name `Equip_Change_Idle_A`. It is not evidence of a missing
general idle-gravity update. The helper bodies were recovered below. The asset
parser maps `+0x28` to `zalign`, not `pony`. Do not treat `placeable="180"` as an angle offset.
Sassy has no XML `jointangle` records.

Ghidra was unavailable during the orientation follow-up. The delegated Opus
session `1233c251-6495-4414-a08a-999d829eec99` reported no running instance and
read cached dumps only after that check. The parent stopped its own Claude
process after the user confirmed the outage. No server was started. The subsequent
offline inspection recovered the helper bodies. The caller/input path of
`0x141693210` remains unresolved. Any resulting orientation change also requires
recapturing and rebaking the motion sample.

The local client Image archive supplied the exact hairstyle icon and all three
placement icons. They were extracted read-only to
`obj/hair-investigation/sassy/orientation-reference/icons/`, with hashes in
`extraction-report.json`. The low-preset icon visibly places the tails beside the
head. These small images are a visual reference, not sufficient transform
evidence. The inspected local Movie archive had no `00200010` path match.

The exact hair scene assignment and entity-property overrides, source/step/dest
schedule, source-buffer interpolation, simulate's extra-second behavior, collision
filters and character collision geometry remain unverified. No alternate solver
or invented spring motion is used. Actual solver identity alone does not establish
client appearance or timing parity. See [the reviewed investigation](HAIR-CLIENT-RESEARCH.md).

## Offline orientation investigation and three-preset comparison

The parent read the existing KMS executable directly using the already installed
pefile and Capstone packages. No executable or Ghidra process was started.
The input is `D:/MS2/KMS2 Debug/x64/MapleStory2.exe`, SHA-256
`e4860f4e3cd8342c52afe29a7658804a567073e0cd9a9da0bf041e1d0e517890`.
The local `orientation-reference/inspect_pe.py` uses PE exception-directory
function bounds and records referenced constant bytes. Outputs include
`helpers.asm.txt`, `placement.asm.txt`, `math.asm.txt`, `equipment.asm.txt`,
`apply.asm.txt` and `controller.asm.txt` in that same ignored directory.

Inspected instructions establish the following additional details:

- `0x141202480` delegates to `0x1412024f0`. The latter reads node transforms,
  obtains direction from a position difference and normalizes it. Its Euler
  output converts radians to degrees and negates the Z result only. This is a
  transform extraction path, not a gravity integrator.
- `0x141695bc0` mirrors a point about a plane. It uses the supplied center's X/Y
  and the point's Z, then subtracts twice the displacement's dot product with
  the supplied normal, multiplied by that normal. The caller must supply a unit
  normal for this to be a reflection. It does not integrate falling motion.
- `0x140a0b080` forms a direction/up basis, converts it to Euler degrees and adds
  90 degrees to Z. The `zalign == 1` branch in `0x141693210` adds another 90. These are specific steps in a
  direction conversion, not evidence for adding 180 to XML placement rotations.
  The direction multiplier at `0x142cf297c` is 10, not a negative factor.
- `0x141422b30` sets supplied position and rotation on nodes. Its additional
  saved-transform composition is guarded by a separate item-data query returning
  13. The earlier description as a general saved hair appearance path was wrong.
  The original hair loading call's inputs still need tracing. Do not count XML
  defaults twice or infer a missing base rotation from this function alone.

`Diagnostics/inspect_sassy_orientation.js` now compares all three existing XML
presets in T3, using the existing EIIie dye fixture and Romantic Wedding Dress.
All 16 checks pass. Six new front/side PNGs were visually inspected, with numeric
bone positions in `orientation-reference/renders/report.json`. At fitting idle
time 0.7, Bone03 is above Bone01 by 0.183/0.212 metres in preset 1, and below it
by 0.107/0.057 metres in preset 3. Preset 2 spreads the tails mostly sideways.
The script leaves preset 3 visible without changing source data or default values.
It renders fixed poses explicitly through `viewer.screenshot()` so a backgrounded
tab's paused requestAnimationFrame does not stall the comparison.

This qualifies the earlier claim that the upward starting pose is necessarily
wrong: it follows the current application of the first authored preset. The
client's final loading transform and runtime appearance have not been reproduced,
so neither an orientation fix nor visual parity is established. Preset 3 is a
source-authored way to preview the lower placement, not a physics correction.
The existing motion sample still supports preset 1 only. Frontend placement,
motion and Sassy tests pass 13 tests; pnpm check reports zero errors and warnings
and completed before the captures. No frontend behavior changed in this pass.

When Ghidra reopened, Opus session `381bf219-aa71-4afc-a570-f7fcbf608887` found
only `/GMS2x64/GMS2x64`, executable `MapleStory2x64_dump.exe`. The supplied KMS
addresses resolved to unrelated functions there. The parent stopped its own
research process and rejected those results as KMS evidence. Eleven recorded
calls were inspection methods, with no mutation calls; the program mismatch
still violated the prompt's KMS-only scope. The transcript and query audit are
in PrivateMaple2's `evidence/sassy-orientation-resumed-queries.json`.

## Reopened KMS program review

The next Opus session used `/KMS2x64/KMS2x64`. The 32 bytes inspected at
`0x141202480` match the local KMS executable above. This verifies the checked
location, not whole-image equality. The three passes recorded 23, 12 and 2
inspection calls, with no mutation calls or permission denials. Raw reports
contain errors; use the reviewed findings in [HAIR-CLIENT-RESEARCH.md](HAIR-CLIENT-RESEARCH.md).

Local RTTI identifies the controller as `CPonyTailController`, derived from
`CItemCustomizeController`. Its `0x141693210` body executes only when the player's
animation name compares equal to `Equip_Change_Idle_A`. The parent verified the
comparison through the local MFC import down to `_mbscmp`. This does not supply
a normal-idle update to make the tails hang.

The asset parser stores `zalign` at `+0x28`, default zero, and `placeable` at
`+0x50`. Each asset owns its custom-preset list at `+0x58`. The custom parser
reads position at `+0x18` and rotation at `+0x24`; its `+0x30` string is the icon.
The list contains placement choices, not the two tails. Sassy's source records
have no `zalign` or `jointangle` attributes. No additional 180-degree adjustment
was found in these parser functions. The copy into saved/runtime hair data and
the final scene-node application are still untraced.

The inspected source NIF has an identity Scene Root and a nearly identity
Point01. Removing that parent does not explain a missing 180-degree rotation.
No placement or motion behavior changed in this research pass. Existing T3
preset comparisons remain the visual evidence; no new render was required for
these documentation-only corrections.
