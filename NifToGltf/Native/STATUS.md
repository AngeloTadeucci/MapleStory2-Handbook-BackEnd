# Simulator status, 2026-09-07

Release candidate **simulator-release-14** is implemented in both repositories on
`feat/clothing-simulator`. The saved hair controls and Sassy static preview are
included in the local hair follow-up commits requested by the user.
No deployment or database writes were performed. Oracle review servers on ports
4000, 4002 and 4003 were stopped at the user's request.

## Coverage

The [Sassy Pigtails follow-up](SASSY-PIGTAILS.md) resolves its second-tail KFM
reference and adds narrow posed NonAccum target handling. A separately authorized
local export now enables both tails at `/outfits?hairPreview=sassy`, using the
existing release-14 base hair. Both glTFs validate and all 59 runtime checks pass;
18 Sassy captures and 20 refreshed snapshot-outfit captures were inspected in T3.
Tails use their rest pose by default. The subsequent [local motion sample](HAIR-MOTION.md)
replays actual client-solver output with explicitly experimental scene settings.
The tails still point upward in that preset-1 sample. A new T3 comparison verifies
that authored preset 3 points downward, with six front/side captures and saved dye
preserved. The local KMS executable supplied more attachment-transform evidence
without Ghidra. Ghidra now has the matching KMS program open. Reviewed code gates
the investigated ponytail customization controller on `Equip_Change_Idle_A`, so
it does not establish a missing general idle-gravity step. The final client
loading path remains unresolved. Playback checks do not establish correct client
hair orientation.
A subsequent user-authorized [browser approximation](BROWSER-HAIR.md) adds
interactive gravity and sway for Sassy, with a simple head collider and bounded
swing. Banded and Cutesy Twin Tails are also available as separate static previews
at `/outfits?hairPreview=twins`, using explicit second-tail KFM references.
The latest full frontend suite passes 155 tests with 15 skipped; typecheck is
clean. The 27 motion checks and 53 twin-tail checks pass, with 32 captures reviewed.
Exact client physics and visual parity remain unimplemented.
Release 14 and the normal catalog remain unchanged.

Local follow-up: [authored hair placement and client evidence](HAIR-PLACEMENT.md)
adds source placement presets for 25 preview hairstyles without changing release
assets. Curled Pigtails has a missing direction texture; physics remains absent.
The [hair follow-up](HAIR-FOLLOWUP.md) corrects saved back/front morph lengths,
adds ponytail size presets, and records decoded client physics constraints.
The [reviewed Claude investigation](HAIR-CLIENT-RESEARCH.md) adds joint XML
defaults and scale bounds. Adjustable hair channels now have continuous controls,
including the verified reverse conversion at the UI boundary. A native diagnostic
loads all 34 authored collision hulls with the client's PhysX 2.8.4 DLL. Browser
physics, final hair-scene settings and missing texture binding remain open. The
notes also record the delegated investigation's earlier Ghidra listing mutation.
The [movable hat correction and client evidence](HAT-PLACEMENT.md) covers Blaze's
and EIIie's reported hat placements without changing the packaged assets.
The [Asthoria dress alias](ASTHORIA-DRESS.md) maps inventory item 12220364 to its
explicit source preset 12220360, whose CL/PA geometry is already in release 14.
The [live outfit alias audit](CATALOG-ALIASES.md) adds Asthoria's five accessories,
Tree's cap, Robbit's stowed scepter and two suppressed Skillet aliases. The
Cherry Blossom Orb alias retains its unsupported animation-target blocker.
The release counts below describe the original packaged catalog.

| Measure | Count |
|---|---:|
| Scoped item IDs | 19,268 |
| Eligible body pairs, all searchable | 33,170 |
| Exported model assets | 9,329 |
| Usable source families | 7,078 / 8,978 |
| Visually inspected pairs | 77 across 42 outfit cases |
| Inherited verified pairs | 80 |
| Preview pairs | 27,788 |
| Unavailable pairs with explicit reasons | 5,302 |

All 10,464 initial model jobs were attempted. No new visual promotions were made.
Conversion and appearance acceptance remain separate. Remaining blockers include
complex attachment rotations/controllers, some hair fitting, Crusader default
colors, paired-star stow overlap, source defects and missing sources/private UGC.

## Verification

45 C#, 17 Python and 115 frontend tests pass. All 9,327 equipment asset cases and
9,329 glTF validators pass. Typecheck reports zero errors or warnings. Build-14c
and compiled browser workflows pass on both bodies, including explicit adjustable
hair reset. Oracle Chromium was used; T3 had no automation host. The detailed dev
workflow passes 17 states, including private Gelo. All 12,146 packaged files match
their hashes. Release-05 and Gelo-07 retain 1,273 unchanged file hashes.

## Testing on another PC

Pull `feat/clothing-simulator` in both repositories. Copy Oracle's complete
`~/repos/MapleStory2-Handbook/static/gltf/simulator-release-14` directory into the
frontend's `static/gltf/`. It is about 5.2 GB and ignored by Git. Preserve the
exported bytes and release inventory; do not regenerate or replace review hashes.
Keep the PC's existing `.env`, icons and private Gelo snapshot. No database import,
GameParser run or migration is needed. Use the normal frontend development command
and open `/outfits`. An existing production preview needs a fresh `pnpm build`.
Private Gelo still requires the development route `/outfits?preview=gelo-07`.

## Evidence and continuation

- [Exact coverage, inspected IDs and evidence hashes](../Diagnostics/wardrobe-release-report.json)
- [Detailed implementation, commands and historical preparation](STATUS-HISTORY.md)
- [Continuation constraints](CONTINUE.md)
- [Simulator plan](SIMULATOR-PLAN.md)

Generated assets, converter checkpoints and captures remain on Oracle. About
25 GiB was free at handoff. Before Oracle commands, source
`~/repos/MapleStory2-Handbook-BackEnd/NifToGltf/obj/oracle-prep/env.sh`.
