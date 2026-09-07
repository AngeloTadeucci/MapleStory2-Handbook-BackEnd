# Asthoria's dress alias, 2026-09-07

The read-only live snapshot has outfit item 12220364 in slot 8, with default
appearance, saved dye colors and no UGC subtype. Local KMS2 itemdata/122.xml
explicitly sets both its originID and itemPreset to 12220360. Its item name is
Romantic Wedding Dress (F). This is an authored item alias, not a guessed outfit
substitution or a private texture replacement.

Source inspected: `D:/Projetos/MapleStory2/XMLs/KMS2/Xml/itemdata/122.xml`.
SHA-256: `968d5d08c1a5332633c3e257489cc8d617aebb983a0319fdb8fe2c63cd43e14c`.
The matching itemmodel/122.xml selects `12220360_F_CLPAWedding03.nif` twice,
once for CL and once for PA, both replacing the female body's matching slots.
Its color palette is 10 and default color index is 16.

Release 14 already includes preset 12220360 with CL asset
`wardrobe-dc4bf9cb8925ea8c81d262a5` and PA asset
`wardrobe-81b92384f7a2271d331682a9`. The catalog omitted inventory ID 12220364.
The frontend catalog parser now adds that exact alias when these two parts are
present. An explicit existing entry always takes precedence. Dye metadata,
preview status and omitted-feature limitations remain unchanged.

No GameParser, database writes, asset conversion or review-hash changes were
needed. The dress's special action and omitted attached features are not newly
implemented by this mapping. Other missing Asthoria equipment remains separate.

Verification: 12 catalog and alias tests pass; svelte-check reports zero errors
and warnings. Tests cover exact asset mapping, dye metadata, CL/PA replacement,
idempotence, existing-entry preservation and different-body/asset exclusions.
The local outfits API returns this alias. Asthoria was rendered in the shared T3
browser with her saved dye; front and side views were inspected. The main
`obj/live-outfits-20260907/Asthoria.png` and report were updated, with backups.
