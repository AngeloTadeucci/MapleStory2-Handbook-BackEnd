# Live outfit catalog aliases, 2026-09-07

The saved `obj/live-outfits-20260907/corrected.zlib.base64` snapshot contains
297 rows for Blaze, EIIie, Chiisa, Skillet, Asthoria, GolemSoldier, Areki, Gelo,
Tree and Robbit. No fresh database access is needed for this audit.

`Diagnostics/audit_live_outfit_aliases.py` searches the supplied local KMS2
itemdata and itemmodel trees. It checks every group 1/2 item against the packaged
catalog for that character's body. This includes gear replaced by outfit slots.
It records complete matching XML definitions, source hashes, native manifest
parts and glTF/dependency hashes in `obj/live-outfits-20260907/alias-audit.json`.
It reads releases without changing assets, inventories or review hashes.

## Explicit mappings

All mappings below come from `tool/@itemPreset`, not names, `originID` or icon
references. Eyewear 11120062 is a useful distinction: its originID is 11120046,
but its actual itemPreset is 11150037. All audited aliases target female entries.

| Inventory ID | Preset | Item | Use in snapshot |
|---|---|---|---|
| 11120062 | 11150037 | Shiny Star Decoration (F) | Asthoria |
| 11320455 | 11320360 | Romantic Wedding Tiara (F) | Asthoria |
| 11620364 | 11620360 | Romantic Wedding Pearl Gloves (F) | Asthoria |
| 11720365 | 11720360 | Romantic Wedding Pearl Shoes (F) | Asthoria |
| 11820364 | 11820360 | Romantic Wedding Party Balloon (F) | Asthoria |
| 12220364 | 12220360 | Romantic Wedding Dress (F) | Asthoria, existing correction |
| 11320621 | 11301544 | Hipster Cat Baseball Cap (F) | Tree |
| 13320221 | 13300220 | Tweet Tweet Pink Bean (Scepter) | Robbit |
| 11250113 | 11250040 | Merry Holiday Earrings (F) | Skillet, replaced by outfit |
| 11850170 | 11850072 | Merry Saintly Fairy Wings (F) | Skillet, replaced by outfit |
| 15600560 | 15600512 | Cherry Blossom Orb | Skillet, unavailable |

The first ten presets have existing geometry and all referenced buffers and
images locally. Their source slots, attachment nodes and NIF names were checked
against itemmodel and the native manifest. The wedding balloon's URN resolves to
the existing 11820359_C_MTWedding03 source bundle. It includes Idle_A animation.

The orb preset has no catalog parts and remains unavailable. Its reason names
unsupported animation targets Main_A, Main_A NonAccum, Move_Point01 and Point01.
Adding its inventory alias exposes the actual blocker without enabling it.

## Frontend behavior

`src/lib/outfits/catalog-aliases.json` holds the audited mappings and exact
expected parts. `catalog.ts` adds an alias only when the matching body, slots,
part IDs and availability are present. An explicit existing inventory entry
always wins, including an unavailable one. Repeated parsing is idempotent.

Aliases preserve source customization, cutting, hair form, geometry, availability,
reason and limitations. Readable names come from local English itemname.xml;
outfit classification comes from itemdata skinType. The dress still occupies
both CL and PA. None of these entries receives appearance-review promotion.

Wedding effects and exporter-omitted features remain limited as recorded by the
presets. Robbit's scepter uses its authored stowed anchor; drawn placement is
still unsupported. Asthoria's weapon 15600541 has a separate animation-target
blocker. The movable-hat correction remains restricted to Blaze and EIIie's
diagnosed assets. Hair physics and missing Curled Pigtails textures are unchanged.

## Unresolved local definitions

The audit found 28 missing item/body pairs in the packaged catalog: ten usable
preset aliases, one unavailable preset alias, and seventeen IDs without a
definition anywhere in the supplied KMS2 itemdata tree. No replacement is inferred
for those seventeen. The full output scopes each finding to these local inputs.

- 12099994: selected ring on Asthoria, Chiisa and EIIie.
- 11950001, 12050001, 12150001: selected pendant, ring and belt on Skillet.
- 11351160, 11451047, 11550951, 11651074, 11751149, 15650537:
  Skillet gear suppressed by outfits, including the full outfit's pants slot.
- 11361344, 11461162, 11561064, 11661245, 11761326: Robbit gear
  replaced by outfit slots.
- 13250281, 14150251: EIIie weapons replaced by outfit slots.

These missing definitions are separate from existing converter failures,
unsupported hair forms, nonvisual Item/Empty.nif items and omitted effects.
Resolving them requires an authoritative client definition that supplies their
actual itemPreset. A matching name alone is insufficient.

## Reproduce the local audit

From this backend checkout on Windows:

```powershell
py NifToGltf/Diagnostics/audit_live_outfit_aliases.py --snapshot NifToGltf/obj/live-outfits-20260907/corrected.zlib.base64 --xml D:/Projetos/MapleStory2/XMLs/KMS2/Xml --names D:/Projetos/MapleStory2/MapleStory2-XML/Xml/string/en/itemname.xml --release ../MapleStory2-Handbook/static/gltf/simulator-release-14 --output NifToGltf/obj/live-outfits-20260907/alias-audit.json
```

Finish frontend `pnpm check` before loading the snapshot into the shared T3
browser. Evaluate `Diagnostics/render_live_outfits.js` asynchronously and poll
`window.liveExport`. The harness captures front and side views, applies saved
dyes after hair fitting, checks dye control values and occupied-slot conflicts,
and records nonvisual items separately. Optional `window.liveRenderNames` limits
the run to named snapshot characters. Never start another development server.

## Local verification

Frontend tests: 127 passed, 15 skipped. `pnpm check` completed with zero errors
and warnings before snapshot injection. Installed-release tests resolve all ten
usable aliases, check their glTF dependencies, and reject the unavailable orb.
Regressions cover explicit-entry precedence, repeated parsing, body/slot/part
guards, unchanged limitations and dye metadata, and CL/PA replacement.

All ten outfits were rendered in the shared T3 browser and all twenty front/side
captures were visually inspected. The final PNGs are 823x650 in the main
`obj/live-outfits-20260907/` folder, with `-side.png` siblings. Capture camera
distance is increased by 8% to avoid clipping foreground tips. All ten final
slot checks pass; 79 exposed color-control checks match saved dyes after posing,
including Gelo's distinct left/right weapon colors. Asthoria's dress occupies
both CL and PA, with no underlying gear pants remaining.

Asthoria's five accessories, Tree's cap and Robbit's stowed scepter render.
The two suppressed Skillet aliases were bundle-tested but were not substituted
into her live outfit for visual review. One Gelo hat request aborted during a
capture pass; the final retry loaded it and has no equipment omissions.

Main PNGs, README and report are current; 22 original files were hash-checked in
`before-accessory-aliases-e37b091e/`. No ZIP was created or updated. The report
contains full omission reasons, nonvisual items, preset limitations, source
audit and capture hashes. Game-client appearance parity was not established.
Production builds have not been rebuilt. No production deployment was performed.
