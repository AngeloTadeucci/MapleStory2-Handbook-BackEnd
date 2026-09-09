# R2 publication preparation, 2026-09-09

The model migration and PC fixes were committed, fast-forwarded into each
repository's default branch, `master`, and pushed:

- Backend application/converter changes: `800ecdb`.
- Frontend: `71357ad`.

The prior remote default revisions were backend `dff68b4` and frontend `46ed795`.
Those are Git revisions, not a verification of the currently deployed application.
The R2 preparation tools and this report are committed separately on `master`.

## Prepared files

All 114,096 originals passed their allowlisted SHA-256 checks. The fresh remote
comparison leaves 97,952 identical objects untouched. The upload delta contains
16,144 objects, 16,509,925,554 bytes, about 15.38 GiB: 7,393 new objects and
8,751 replacements. Every replacement has verified rollback bytes and freshly
captured serving metadata. No backup is missing, and no remote deletion is planned.

The upload package is in Ubuntu on E:, at:

```text
/home/ubuntu/repos/MapleStory2-Handbook-BackEnd/NifToGltf/obj/r2-publish-20260909
```

Windows can access it at:

```text
\\wsl.localhost\Ubuntu-24.04\home\ubuntu\repos\MapleStory2-Handbook-BackEnd\NifToGltf\obj\r2-publish-20260909
```

Its source is the tested `pc-glasses-canonical-2` candidate. Only `assets/` is
upload content. `publish-plan.json` records exact object keys, SHA-256 and MD5,
MIME types, cache policy, and ordered groups. `rollback/` contains the previous
bytes and captured serving metadata for replacements. The complete R2 listing,
source revisions, and CDN preflight evidence are retained beside the plan.

`preview-upload.ps1` rehearses the ordered copies with `--dry-run` always enabled.
The plan uses `rclone copy`, never sync or purge. Dependencies precede models,
then auxiliary metadata, the native manifest, simulator catalog, and inventory.
All uploaded mutable objects use `public, max-age=0, must-revalidate` and explicit
MIME types. Existing unchanged objects retain their headers. Build compression
sidecars, private previews, and old release folders are outside the upload delta.

## Verification and cutover

`prepare_r2_publish.py` runs in WSL with the other migration diagnostics. It
hashes every allowlisted original, compares MD5 against a fresh R2 listing, and
checks replacement backup bytes against their SHA-256 record and current R2 MD5.
Its finalization step requires current metadata for exactly the replacements and
rejects remote content drift. Failed revalidation clears the prepared flag.
Three focused tests cover the delta, rollback matching, source/path rejection,
metadata capture, and stale remote detection.

The existing public CDN sample returned `Access-Control-Allow-Origin: *`. Its
legacy glTF Content-Type was `application/octet-stream`; uploads explicitly use
`model/gltf+json`. This sample does not establish every object's CDN behavior.
See [rclone S3 metadata](https://rclone.org/s3/#metadata) for the header options.

Before actual publication, revalidate the remote snapshot and identify the
deployed frontend revision. Canonical replacements require a coordinated viewer
cutover or maintenance window. Invalidate changed CDN keys and verify public
content, MIME, cache headers, and CORS after upload. The package README records
upload order and rollback handling, including restoration of absent headers.
Keep Noesis and legacy fallbacks. The candidate still has unavailable entries
and lacks full game-client appearance acceptance.

No R2 upload, deletion, CDN purge, or application deployment was performed by
this preparation. The compiled local preview remains on port 4011.
