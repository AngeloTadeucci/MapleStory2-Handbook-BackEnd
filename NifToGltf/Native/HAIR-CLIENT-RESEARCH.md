# Reviewed Claude client investigation, 2026-09-07

The user authorized a separate Claude CLI investigation launched from
`D:/Projetos/MapleStory2/PrivateMaple2`. It queried the existing Ghidra MCP with
`program="KMS2x64"`; the open GMS2 dump was not used for these addresses.
Database and shell tools were excluded. The initial investigation changed no
program annotations, repository code, assets, servers or production state.
The continuation had a Ghidra listing mutation, documented below.

Raw reports are under that checkout's ignored `.scratch/handbook-hair-ghidra/`:
`findings.md`, `errata.md`, both stream logs, and
`evidence/all-query-results.json`. Claude session:
`18893f89-050b-4f6c-ae27-8e8a06e30c6f`.

**Read the errata with the first report.** Independent review found incorrect
zero classification, a limit/drive offset mix-up, a frame-array interpretation,
and confusion between stored defaults and missing-data fallback. Claude's
correction pass withdrew those claims. This document records the reviewed result.

## Confirmed findings

### Joint XML and defaults

`0x140543ca0` parses at most four jointangle records per asset, with stride
`0x18`: name at `+0`, soft at `+8`, posz/zero/negz at `+0xc/+0x10/+0x14`.
The asset constructor `0x140543a90` also constructs exactly four records.
The model callback `0x14143fe10` invokes the helper for the asset's pony flag.

Element constructor `0x140543a30` defaults soft to `DAT_142d175c4`, bytes
`0a d7 23 3c`, float32 approximately `0.01`. All three angles default to zero.
The XML reader only assigns successfully read attributes, so missing soft is
not a no-op or an implicit zero.

`0x1416962d0` converts posz/zero/negz from degrees and passes them as XYZ angles
to the same rotation routine used for attachment placement. They reorient the
joint frame; they do not select static skeleton poses or alter limit angles.
The writes go to descriptor `0x20` and `0x38`. Its validation loop addresses
separate arrays with stride 12: `0x20/0x2c` and `0x38/0x44`. This supports
actor 0's axis/normal interpretation. Actor 1's vectors remain authored. The
array indexing is verified; the field names come from the PhysX layout inference.

### Limits and validity

`0x141696ea0` calls `0x141696de0` at descriptor offsets
`0xa8/0xb8/0xc8/0xd8/0xe8`. The latter assigns soft to the first float of each
16-byte record and zeroes the other three floats. These are the three single
limits and the two halves of the limit pair. The six drive records begin at
`0xf8`, so this helper does not alter drives.

The local NifSkope schema names the limit fields value/restitution/spring/damping.
The subsequent SDK-header and native-probe check confirms this layout. Angular
values use radians, linear values use distance, and zero spring means a hard
limit. See the native probe section below.

`0x1409a8f90` evaluates `(_fpclass(value) & 0x207) == 0`. It accepts finite values,
including both signed zeros. A local Windows CRT check verified zero, normal,
NaN and infinity cases. Positive zero returned `0x40`, negative zero `0x20`, and
positive infinity `0x200`. The initial report incorrectly treated `0x200` as zero.
See Microsoft's [_fpclass reference](https://learn.microsoft.com/en-us/cpp/c-runtime-library/reference/fpclass-fpclassf?view=msvc-170)
for the classification categories.

Consequently a matched record with omitted soft still supplies approximately
0.01 to all five limit records. The raw NIF's 15/25/40-degree Curled Pigtails
swing limits are source values, not established final client limits after this
helper runs. Do not feed the raw limits directly into a simulator and claim parity.

The helper caches unscaled anchors under joint-name-plus-actor-index keys and
recomputes each anchor from that cache and equipment scale. Repeated size changes
must not compound the current anchor. Descriptor writes occur between calls
`0x14168a540` and `0x14168a570`; the precise scene synchronization implementation
still needs investigation.

### Scale records

Constructor `0x140544b10` defaults max to 1, min to 0, reverse to 0, and the value
list to empty. Parser `0x140544b70` reads max at `+0`, min at `+4`, reverse at
`+8`, and the preset list at `+0x10`. These are independent fields. A single
preset therefore does not prove that the source range is fixed.

`0x140544f20` retrieves a scale by document-order index and returns a default
record for an absent index. `0x141411920` obtains per-channel bounds when loading
stored values into the customization UI. The later beauty-shop trace below
confirms the reverse conversion as `max - value + min` at the UI boundary.

`CHairExtraData` at `0x14059a8a0` initializes its two stored lengths to zero.
`0x141422340` returns 1 when the required appearance data cannot be reached.
Those are different cases. The current direct saved-value import remains valid;
the initial report's instruction to treat 1 as the stored default was retracted.

### Curled Pigtails material

The declaration at `0x142767180` passes `(mapIndex=1, defaultName=NULL)` to
`0x141d81630`. The latter stores the map index and optional name; it is not the
texture binding routine. HLSL samples Shader1 for direction, but the no-map
binding or permutation path remains unknown. No substitute texture is justified
by this evidence, so Curled Pigtails remains unavailable.

## Decision and next investigation

Physics implementation remains on hold. The investigation did not establish
scene gravity, stepping/substeps, collision settings, cooked shape handling,
source/destination update order, or whether FlowerUp receives generated bodies.
The helper studied here reconfigures existing joints and does not answer the
body-creation question. No approximate browser physics was added.

The later passes below resolve scale reverse and cooked collision loading.
Final scene settings, execution order and material binding still need evidence.
No new game-client visual-parity claim follows from this static investigation.

## Continuation: scale controls and remaining physics gaps

The next pass ran from the same PrivateMaple2 folder and Claude session. Its
report is `continuation-findings.md`, with 75 returned tool results in
`evidence/continuation-queries.json`. Treat the raw report as research notes.
The conclusions below were checked against returned decompilation/disassembly
and local XML. In particular, the customization UI path is not the ordinary
saved-appearance render path; do not generalize its clamp to all rendering.

### Implemented scale ranges

The customization controller stores clamped values at `+0x38 + 4*index`.
`0x141417030` obtains min/max from record `+4/+0`. The two leaves
`0x140a0fda0` and `0x140a0fde0` select max and min respectively, completing
the clamp chain. Getters `0x140b02c40/0x140b02c60` read those stored values;
`0x141417c10` passes them to the morph update. None of these operations uses
the preset list. This supports continuous values within the source bounds,
including channels with one preset. It does not prove a particular slider
step in the client; the handbook uses 0.01 for its own UI.

`0x140546a90` parses customize.scale as a bool at `+0x48`, beside rotation,
translation and capAttach. The later UI option builder at `0x140ccb0c0` reads
the scale getter `0x140547130` and produces option bits 1 or 2. The handbook
requires explicit scale="1" before offering controls. Stored appearance import still applies
independently of this UI permission and does not clamp saved values.

`Diagnostics/hair_scale_metadata.py` extracts 255 source definitions and their
source SHA into frontend `hair-scale-source.json`. It does not touch release
assets or inventory. Of 258 packaged item/body entries with hairScales, 256
match this XML's presets and permission flag. Unkempt Hair 10200019 and
Classic Perm 10200025 have packaged `[[]]` but no XML scale records; they
receive no new range control. Each channel checks its packaged presets before
using the local range. The later trace resolves reverse and enables those range
controls with the client's UI conversion.

### Material and scene follow-up

All nine locally installed MS2CharacterHairMaterial pixel shaders, Shader0000
through Shader0008, sample Shader1 and use CalculateHairTangentFromColor.
The shared material declaration also has a NULL default for ColorOverrideMap.
A NULL name alone therefore does not establish an error or fallback.
Draw-time missing-map resolution is still untraced. Curled Pigtails remains
unavailable, without a replacement texture.

`0x14168a540` calls `0x14168b270`, which calls `0x140d852c0` and reaches
EnterCriticalSection. Its counterpart calls `0x14168b240/0x140d86210`.
This establishes a critical-section entry rather than a simulation pause.
The step loop, gravity, collision configuration and cooked-mesh handling
remain unknown. FlowerUp's generated-body question is also unresolved:
`0x1416952c0` applies transforms and tests `0x141200ab0(...)+0x20`, whose
meaning still needs tracing. No physics solver was added.

### Ghidra listing mutation during delegation

The continuation called `mcp__ghidra__disassemble_bytes` at `0x14274e7d0`
for 220 bytes. It returned success through `0x14274e8ab`. This violated the
read-only instruction by defining instructions in the KMS2x64 analysis listing.
The parent had mistakenly allowed a broad `disassemble*` tool pattern.
The run was stopped after this was noticed, the pattern was removed, and
disassemble_bytes was explicitly denied. A final pass used named inspection
tools. No byte-patching call appears in the captured log.

The original listing state was not captured, so no guessed rollback was made.
The analysis listing change remains unreverted. This incident does not justify
claiming the Ghidra project was unchanged. No release assets, database state,
servers, repository code or executable patching were part of the delegated work.

## Separate focused Opus passes

The subsequent runs used `claude-opus-5` with high effort, launched from the
authorized PrivateMaple2 directory. Sessions are:

- Reverse: `faedfd79-eb39-4325-be58-37b190ecacd6`.
- Physics: `e0815be1-3970-49fb-952c-fed843fe0373`.
- Material: `f770fe65-62ef-46b4-aa20-cbc2823c1229`.

Before these passes, 34 on-disk Ghidra project files were copied with SHA-256
verification, totaling 8,305,559,832 bytes, to
`.scratch/handbook-hair-ghidra/project-before-three-investigations-20260907/`.
This was an on-disk copy while Ghidra was open; it does not capture unsaved
in-memory analysis. It does not undo the earlier listing mutation.

A local bridge copy now exposes an explicit inspection-tool allowlist. No
wildcard disassembly permission remains. Script execution, program opening,
imports, byte patching, listing definition and annotation changes are excluded.
No new listing mutation appears in the captured focused-pass calls.

### Reversed UI values

The client SWF was extracted read-only from the existing Gfx archive and
decompiled locally using FFDec. `CustomOption.as` passes min/max/cur to sliders
and sends OnChangeSize with the channel ID and slider value. Its TileList
renderReverse property is unrelated to the hair scale flag.

The C++ dispatcher `0x1418c4550` sends OnChangeSize to `0x1418c78d0`. Assembly
at `0x1418c7971` tests the scale record's reverse field at +8. When nonzero,
the handler computes `max - UI value + min` before calling the setter. The
outbound size-data builder at `0x140f32a70` performs the same conversion on
stored values. Character creation mirrors this in `0x1418e7110` and
`0x1418e5a10`. This establishes both directions of the conversion, without
changing the meaning of stored appearance floats.

Frontend unit tests cover above-one bounds, endpoints, validation, raw saved
import and exported reset defaults. Actual shared T3 renders verify
Anti-Gravity Braids 10200067 at displayed 1.1/raw 0.8 and displayed 1.3/raw 0.6
from front and side. Reset returns raw 1/displayed 0.9. The first reverse pass's
claim that no runtime consumer exists was withdrawn after the handlers were found.

### Physics: decoded shapes and unresolved scene configuration

The local [native probe](PHYSX-PROBE.md) uses pinned NVIDIA 2.8.4 headers and
the client's existing 2.8.4.6 core DLL. It confirms the D6 offsets used by
`0x141696ea0` and reads all 34 cooked collision hulls completely. This resolves
the former missing-header and opaque-hull gaps. These files remain diagnostic
evidence outside release inventories.

`0x1422646b0` initializes PhysXSceneData with timestep approximately 0.033333,
budget 8, divisor 1 and gravity component -9.8. These are constructor defaults,
not established final hair-scene settings. `0x1420651f0` copies scene-record
scale/timing values into the prop-service scene wrapper. The writers selecting
the final hair-scene record values remain untraced.

`0x142265520` calculates a step count with ceil after a roughly 1e-6 rounding
guard, divides the timestep, caps total substeps by its budget, calls setTiming,
then passes `totalSubsteps * substepSize + 1.0f` to simulate. The +1.0 is present
in assembly. NVIDIA's 2.8 [timing documentation](https://www2.denizyuret.com/bib/nvidia/PhysX28/PhysXDocumentation.pdf)
and NxScene.h say time above the substep budget accumulates. The initial claim
that this extra time was discarded was wrong. Its eventual effect in the hair
scene has not been verified; do not simplify it into an assumed 30 Hz loop.
FetchResults at `0x142264d10` commits the pending timestamp when results finish.

`0x142062e00` registers source and destination tasks with priorities 1400 and
1320/1820. Priorities alone do not establish execution order. The scheduler's
ordering and the flag selecting the destination priority remain unknown.

`0x14143fe10` supplies group 0 and four zero mask words to the existing-prop
helper. `0x141689d00` forwards the group and mask to each actor. This does not
establish the scene's filter operations or the resulting collision policy.
`0x14168a5a0` caches default and item scenes at indices 0 and 1. A later inspection
of `0x14168a540` shows it forwards only the SDK-manager pointer to a global lock;
the extra call-site argument is not a scene index. The earlier inference from
that argument is withdrawn. The actual scene-name write for hair is unconfirmed.

The SDK parameter writes in `0x142063180` are debug visualization settings.
Nxp.h identifies parameter 9 as NX_VISUALIZATION_SCALE, and the other nine as
body/joint/collision visualization options. They are not solver configuration.
The first runtime report's stronger interpretation was withdrawn.

Only matched jointangle records receive the limit override. Their soft value
sets each limit's value and zeroes restitution, spring and damping. Angular
limits use radians; the linear limit uses distance. An omitted soft defaults
to approximately 0.01. Explicit XML values are not replaced by that default.
Calling all five limits angular or every joint nearly rigid would be incorrect.

No browser physics implementation follows from these diagnostics. A compatible
legacy solver execution route, actual scene settings, update order and running
client comparison remain necessary.

### Material findings requiring caution

The early reports repeatedly generalized setup-time or name-lookup behavior to
draw-time binding. Those conclusions were rejected during review. In particular:

- `0x141cabdc0` is called by NiBinaryShaderLibrary setup. It is not a device bind.
- `0x1427f2600` is a state-container setter that supports null entries.
- `0x1427d1ff0` creates procedural renderer defaults. A filename search cannot
  prove that no fallback exists. Its two-pixel texture is not established as the
  missing hair-direction map.
- MS2DefaultTexturePalette lookup is name-keyed. `0x141553eb0` returns an empty
  handle for an empty name. That describes this API, not every possible fallback.
- `0x1414ea8b0` reads D3D9 render and texture-stage state for diagnostics. It
  establishes a device identity but is not the draw-time binder.

The inspected raw Curled Pigtails tail NIFs omit shader texture 1. All nine
installed hair shaders sample it. Whether the client supplies a deterministic
default or binds null remains unconfirmed. Curled Pigtails stays unavailable;
no other hair's direction map has been substituted. The parent found candidate
device calls through read-only PE byte inspection after the bridge lacked a
byte-search tool; the resulting addresses and executable hash are in
`obj/hair-investigation/device-call-sites.json`.

### Device binder reached, Curled-specific connection still open

The candidate at `0x1427de0a7` reaches the generic texture binder
`0x1427ddfc0`. It initializes the resolved texture to null and only resolves a
texture when the stage object's absolute +0x18 field is nonzero. It compares
that result against its shadow cache, then calls device SetTexture at vtable
+0x208 with the stage from +0x10 and the resolved pointer. A null result returns
immediately afterward. Adjacent SetTextureStageState, SetSamplerState and
SetRenderState calls support the device identity. `0x1427df410` and
`0x1427df580` also clear trailing stages. This is verified generic binding
behavior, conditional on the tracked cache state.

The final setup pass retracts an earlier assertion that every empty slot is
skipped: `0x141fded30` supports creating missing stage records, and setup calls
it with creation enabled. However, this does not establish every subsequent
copy or the Curled-specific draw path. The returned constructor decompilation
uses NiRefObject_data-relative offsets, which must not be mistaken for absolute
object offsets. The stage draw-loop caller was not recovered in that pass.

All nine local hair HLSL files declare `sampler2D Shader1` without an explicit
register. Therefore the name Shader1 does not prove it occupies register s1.
The compiled shader's register mapping and the actual Curled material stage
still need to be connected. The raw final-hop report's claim that the entire
setup-to-bind path is closed is stronger than its evidence and is not adopted.
No null-sample material substitute was enabled.

The reviewed generic binder and setup evidence is retained in
`evidence/material-candidates-queries.json` and
`evidence/material-final-hop-queries.json` under the PrivateMaple2 scratch folder.
The focused-pass audit found one out-of-allowlist inspection request,
list_tool_groups, which was denied. No mutation call was found in these runs.

## Sassy motion follow-up

Claude Opus session `a2e642e0-aba3-47d1-9641-6c63b2dea911` ran from PrivateMaple2
through the explicit CLI inspection allowlist. The audit records 62 calls, with
zero permission denials and no mutation calls. The raw report claims 33 queries;
that count does not match the recorded calls. Evidence is in
`.scratch/handbook-hair-ghidra/evidence/sassy-motion-queries.json`.

The parent checked returned decompilation for `0x1414d7990`: it constructs an
instance prefix and appends the scene identifier. This supports treating
PhysXDefaultSceneName and PhysXItemSceneName as scene names. It does not prove
that no entity data can override scene properties. The report's summary says
settings are never overridden, while its uncertainty section admits property
writers were not enumerated. That stronger absence claim is not adopted.

The parent also checked `0x14168a540` and `0x14168b270`: these forward the SDK
manager and lock its critical section. Their extra caller argument is not scene
selection. The earlier default-scene inference is withdrawn above.

Returned `0x141ef8550` reads actor global poses. With a parent actor, it reads
both poses, scales translations, and calls `0x141ef8a20` to form the destination
transform. Rotation entries are copied separately. This supports a world-to-local
playback adapter, but does not establish the complete client task schedule.

The matched-joint path in `0x1416962d0` builds normal/axis from rotated canonical
X/Z vectors and replaces actor 0's frame. The parent checked the relevant returned
calls. However, local Sassy XML contains no jointangle records at all. The prompt
and returned Sassy-specific recommendation were wrong on that point. No joint
frame or soft-limit override is applied to the Sassy motion experiment.

The local [motion sample](HAIR-MOTION.md) uses the actual client DLL, source hulls,
actors and D6 joints. Its fixed-step timing and gravity are explicitly experimental.
Scene assignment, actual property overrides, scheduler order, the simulate extra
second, source buffering, filtering, and running-client comparison remain open.

## KMS reopened: placement review

Opus session `797821b0-8884-41f3-9d56-c6fc05b29d21` ran from PrivateMaple2
against the existing `/KMS2x64/KMS2x64` program. The listing and 32 inspected
bytes at `0x141202480` identify the checked KMS location. They do not establish
whole-image equality with the local executable. Three passes recorded 23, 12
and 2 inspection calls, with no permission denials or mutation calls. Their
query audits are `evidence/sassy-kms-loading-20260908-queries.json`,
`evidence/sassy-kms-loading-review-20260908-queries.json` and
`evidence/sassy-kms-exact-20260908-queries.json`. Decompilations and raw reports
are in `evidence/sassy-kms-loading/` under the same ignored scratch directory.

The parent reviewed raw code and local PE instructions. These findings survive
that review:

- RTTI for vtable `0x142f4e310` identifies `CPonyTailController`, derived from
  `CItemCustomizeController`. Function `0x141693210` occupies slot `+0x100`.
- That function's body requires equality with animation name
  `Equip_Change_Idle_A`. The wrapper `0x1402a5b40` returns Compare != 0 and the
  caller enters on false. The parent traced imported MFC ordinal 2899 in the
  local `mfc140.dll` to `_mbscmp`, independently confirming equality semantics.
  This body is not an unconditional idle-gravity step. Other dispatch and the
  complete normal hair update schedule remain untraced.
- Asset parser `0x140543ca0` reads `zalign` at `+0x28`, `placeable` at `+0x50`
  and a per-asset custom list at `+0x58`. Constructor `0x140543a90` initializes
  `zalign` to zero. Custom parser `0x140266aa0` reads position at `+0x18`,
  rotation at `+0x24` and the icon string at `+0x30`. Each Sassy tail has three
  placement choices; the first two list entries are not the two tails.
- The saved-transform branch in `0x141422b30` requires the separate query
  `0x14053c620` to return 13. That query obtains a different item object through
  `0x1411dc7f0`, `CItem::GetData` and `0x140527840`. Equal numeric offsets do not
  make it the asset parser's `zalign` field. Do not use this conditional branch
  as evidence of general saved hair transforms. Group 1/type 13 is hat in the
  repository's ItemType model; completing the client metadata producer trace
  would establish that interpretation independently.
- Function `0x141b6dea0` unpacks integer bytes and multiplies them by 1/255.
  The values supplied through `0x14143e3e0` are packed colors, not hair lengths
  or placement vectors. Its three-entry loop does not establish tail count.
- The source NIF's Scene Root is identity and Point01 is nearly identity. A
  discarded 180-degree parent rotation is not present in these inspected nodes.

The reports require explicit corrections. Bytes `00 00 b4 42` represent 90.0,
not 180.0. The first report inverted the animation guard in prose. The second
report conflated asset and item objects, color and length data, and custom-list
entries and tails. Its CHairExtraData offsets are relative to a decompiler
substructure, not verified absolute object offsets. The parent requested exact
functions after repeated interpretation errors and adopted no transform patch
from those reports.

The remaining placement gap is the transfer from the selected XML custom record
or saved hair appearance into the model's initial transforms. The inspected
Point01 lookup `0x1411dd700` establishes a node search, not that transfer.
Neither these parser functions nor the customization controller establish the
final normal-idle pose. No frontend behavior, release asset or motion bake
changed in this pass. The preceding T3 three-preset comparison remains the
visual evidence, with its limitations recorded in [HAIR-MOTION.md](HAIR-MOTION.md).
