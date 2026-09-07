# Simulator status, 2026-09-07

Release candidate **simulator-release-14** is implemented in both repositories on
`feat/clothing-simulator`. The user authorized committing and pushing this work.
No deployment or database writes were performed. Oracle review servers on ports
4000, 4002 and 4003 were stopped at the user's request.

## Coverage

Local follow-up: [authored hair placement and client evidence](HAIR-PLACEMENT.md)
adds source placement presets for 25 preview hairstyles without changing release
assets. Curled Pigtails has a missing direction texture; physics remains absent.
The [movable hat correction and client evidence](HAT-PLACEMENT.md) covers Blaze's
and EIIie's reported hat placements without changing the packaged assets.
The [Asthoria dress alias](ASTHORIA-DRESS.md) maps inventory item 12220364 to its
explicit source preset 12220360, whose CL/PA geometry is already in release 14.
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
