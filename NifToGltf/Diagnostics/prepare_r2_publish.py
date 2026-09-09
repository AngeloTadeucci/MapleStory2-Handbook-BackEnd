"""Prepare a local upload delta from an allowlist and a read-only R2 listing.

Hashes every local original. Never uploads, deletes remote objects, or includes
build sidecars/private files. Remote metadata must be refreshed before cutover.
"""
import argparse
from collections import Counter, defaultdict
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
from migration_inventory import read, write, safe_path


CACHE_CONTROL = 'public, max-age=0, must-revalidate'
REMOTE = 'r2:handbook-gltfs/'


def phase(path):
    if path == 'native-manifest.json': return 40
    if path == 'simulator-catalog.json': return 50
    if path == 'model-files.json': return 60
    if '/' not in path: return 30
    return 20 if path.endswith(('.gltf', '.glb')) else 10


def content_type(path):
    return {'.gltf': 'model/gltf+json', '.glb': 'model/gltf-binary', '.zip': 'application/zip',
            '.json': 'application/json', '.png': 'image/png',
            '.jpg': 'image/jpeg', '.jpeg': 'image/jpeg', '.webp': 'image/webp',
            '.bin': 'application/octet-stream', '.dds': 'image/vnd-ms.dds'}[Path(path).suffix.lower()]


def prepare(models, listing, backup, output):
    if output.exists(): raise ValueError('Choose a fresh publication directory')
    inventory = read(models / 'model-files.json')
    if inventory.get('version') != 1: raise ValueError('Expected version 1 allowlist')
    remote_rows = read(listing)
    remote = {r['Path']: r for r in remote_rows}
    if len(remote) != len(remote_rows): raise ValueError('Duplicate remote object')
    recovery = read(backup / 'backup.json')
    if not recovery.get('complete') or recovery.get('remote') != REMOTE:
        raise ValueError('Expected a complete recovery manifest for the target bucket')
    previous = {r['path']: r for r in recovery['files']}
    cache = {}
    def hashes(path):
        if path.is_symlink() or not path.is_file(): raise ValueError('Expected physical original: ' + str(path))
        stat = path.stat()
        key = (stat.st_dev, stat.st_ino, stat.st_size, stat.st_mtime_ns, stat.st_ctime_ns)
        if key not in cache:
            sha, md5 = hashlib.sha256(), hashlib.md5()
            with path.open('rb') as stream:
                for chunk in iter(lambda: stream.read(1024 * 1024), b''):
                    sha.update(chunk); md5.update(chunk)
            after = path.stat()
            if (after.st_dev, after.st_ino, after.st_size, after.st_mtime_ns, after.st_ctime_ns) != key:
                raise ValueError('File changed while hashing: ' + str(path))
            cache[key] = (sha.hexdigest(), md5.hexdigest())
        return cache[key]
    records = list(inventory['files'])
    seen = set()
    for record in records:
        key = record['path']
        safe_path(key)
        if key in seen or key == 'model-files.json' or re.search(r'[\r\n\x00]', key) or key.endswith(('.gz', '.br')) or re.match(r'(character-previews|simulator-release-\d+|.*preview[^/]*|gelo[^/]*)/', key, re.I):
            raise ValueError('Invalid, duplicate, or private allowlist path: ' + key)
        seen.add(key)
        content_type(key)
    records.append({'path': 'model-files.json', 'bytes': (models / 'model-files.json').stat().st_size,
                    'sha256': hashes(models / 'model-files.json')[0]})
    output.mkdir(parents=True)
    uploads, unchanged, rollback, missing_backup = [], [], [], []
    for index, record in enumerate(records):
        key = record['path']; source = models / key
        if not source.resolve().is_relative_to(models.resolve()): raise ValueError('Source path escapes package')
        digest, md5 = hashes(source)
        if source.stat().st_size != record['bytes'] or digest != record['sha256']:
            raise ValueError('Canonical bytes changed: ' + key)
        current = remote.get(key)
        if current and current.get('Hashes', {}).get('md5') == md5 and current['Size'] == record['bytes']:
            unchanged.append(key)
        else:
            row = dict(record, md5=md5, action='replace' if current else 'create', phase=phase(key),
                       contentType=content_type(key), cacheControl=CACHE_CONTROL, previous=current)
            uploads.append(row)
            target = output / 'assets' / key
            target.parent.mkdir(parents=True, exist_ok=True)
            os.link(source, target)
            if current:
                old = previous.get(key)
                old_file = backup / 'objects' / key
                if old and old_file.is_file():
                    old_sha, old_md5 = hashes(old_file.resolve())
                    if old_sha == old['sha256'] and old_file.stat().st_size == current['Size'] and old_md5 == current.get('Hashes', {}).get('md5'):
                        destination = output / 'rollback/objects' / key
                        destination.parent.mkdir(parents=True, exist_ok=True)
                        os.link(old_file.resolve(), destination)
                        rollback.append(dict(old, md5=old_md5, current=current))
                        continue
                missing_backup.append(key)
        if index % 5000 == 0: print(f'Hashed {index + 1}/{len(records)} originals', flush=True)
    groups = defaultdict(list)
    for row in uploads: groups[(row['phase'], row['contentType'])].append(row['path'])
    upload_groups = []
    for number, ((order, mime), keys) in enumerate(sorted(groups.items())):
        filename = f'lists/{order:02}-{number:02}.txt'
        path = output / filename; path.parent.mkdir(exist_ok=True)
        path.write_text(''.join(k + '\n' for k in sorted(keys)))
        upload_groups.append(dict(phase=order, contentType=mime, files=filename, count=len(keys)))
    for name, keys in [('unchanged', unchanged), ('replace', [r['path'] for r in uploads if r['action'] == 'replace']),
                       ('create', [r['path'] for r in uploads if r['action'] == 'create']), ('missing-backup', missing_backup)]:
        (output / (name + '.txt')).write_text(''.join(k + '\n' for k in sorted(keys)))
    write(output / 'rollback/backup.json', dict(version=1, remote=REMOTE, files=rollback,
          complete=not missing_backup, metadataFresh=False, note='Content verified against current remote MD5; refresh serving metadata before publication'))
    plan = dict(version=1, preparedAt=datetime.now(timezone.utc).isoformat(), remote=REMOTE,
                source=str(models.resolve()), inventorySha256=records[-1]['sha256'], remoteListing=str(listing.resolve()),
                modelFiles=len(records), sourceBytes=sum(r['bytes'] for r in records),
                unchanged=len(unchanged), actions=dict(Counter(r['action'] for r in uploads)),
                uploadFiles=len(uploads), uploadBytes=sum(r['bytes'] for r in uploads),
                rollbackFiles=len(rollback), missingBackup=missing_backup, metadataFresh=False,
                cacheControl=CACHE_CONTROL, deleteRemoteObjects=False, groups=upload_groups, files=uploads)
    write(output / 'publish-plan.json', plan)
    print(json.dumps({k:v for k,v in plan.items() if k not in ['files','groups']}))
    return plan


def finalize(output, metadata):
    plan = read(output / 'publish-plan.json')
    plan.update(prepared=False, metadataFresh=False)
    write(output / 'publish-plan.json', plan)
    rows = read(metadata)
    current = {r['Path']: r for r in rows}
    if len(current) != len(rows): raise ValueError('Duplicate metadata object')
    replacements = {r['path']: r for r in plan['files'] if r['action'] == 'replace'}
    if set(current) != set(replacements): raise ValueError('Metadata does not cover exactly the replacements')
    for key, row in replacements.items():
        now, before = current[key], row['previous']
        if not now.get('Hashes', {}).get('md5') or now['Hashes']['md5'] != before.get('Hashes', {}).get('md5') or now['Size'] != before['Size']:
            raise ValueError('Remote content changed during preparation: ' + key)
        if 'Metadata' not in now: raise ValueError('Serving metadata was not captured: ' + key)
    recovery = read(output / 'rollback/backup.json')
    if plan['missingBackup'] or not recovery['complete']: raise ValueError('Current rollback bytes are missing')
    for row in recovery['files']:
        row['remote'] = current[row['path']]
        row.pop('xattrsBase64', None) # Fresh remote metadata owns restoration headers.
    captured = datetime.now(timezone.utc).isoformat()
    recovery.update(metadataFresh=True, metadataCapturedAt=captured,
                    note='Content matches remote MD5 and serving metadata was captured during preparation. Recheck for remote drift before cutover.')
    write(output / 'rollback/backup.json', recovery)
    write(output / 'replacement-metadata.json', rows)
    shutil.copyfile(plan['remoteListing'], output / 'remote-listing.json')
    plan.update(metadataFresh=True, metadataCapturedAt=captured,
                replacementMetadataSha256=hashlib.sha256((output / 'replacement-metadata.json').read_bytes()).hexdigest(),
                remoteListingSha256=hashlib.sha256((output / 'remote-listing.json').read_bytes()).hexdigest(), prepared=True)
    write(output / 'publish-plan.json', plan)
    (output / 'preview-upload.ps1').write_text('''# Read-only upload rehearsal. This script always uses --dry-run.
param([string]$PackageRoot = $PSScriptRoot)
$ErrorActionPreference = 'Stop'
$plan = Get-Content (Join-Path $PackageRoot 'publish-plan.json') -Raw | ConvertFrom-Json
if (-not $plan.prepared -or $plan.remote -ne 'r2:handbook-gltfs/') { throw 'Unprepared or unexpected destination' }
foreach ($group in $plan.groups | Sort-Object phase, files) {
    & rclone copy (Join-Path $PackageRoot 'assets') $plan.remote --dry-run --ignore-times --no-traverse --s3-no-check-bucket --files-from-raw (Join-Path $PackageRoot $group.files) --metadata --metadata-set "content-type=$($group.contentType)" --metadata-set "cache-control=$($plan.cacheControl)"
    if ($LASTEXITCODE -ne 0) { throw "Rehearsal failed for $($group.files)" }
}
''')
    (output / 'README.md').write_text(f'''# Prepared R2 model package

Destination: `{REMOTE}`. No remote writes were performed.

The explicit allowlist contains {plan['modelFiles']:,} originals. This delta has
{plan['uploadFiles']:,} uploads, {plan['uploadBytes']:,} bytes:
{plan['actions'].get('create', 0):,} new objects and {plan['actions'].get('replace', 0):,} replacements.
{plan['unchanged']:,} identical remote objects stay untouched. There are no deletions.
Only `assets/` is upload content; reports, rollback data, private assets, and build
compression sidecars must not be uploaded. Every original was checked against its
SHA-256 allowlist; upload and rollback MD5 hashes are recorded in the manifests.

`publish-plan.json` records exact keys, hashes, MIME types, cache headers, and ordered
groups. `rollback/objects/` and `rollback/backup.json` retain each replaced object's
bytes and current serving metadata. Revalidate remote hashes and metadata immediately
before cutover because this is a preparation snapshot.

Run `preview-upload.ps1` from Windows for a dry run. It always passes `--dry-run`.
When the package is accessed through the WSL network path, copy the script to a
local Windows folder and pass `-PackageRoot` with the full WSL package path.
When publication is authorized, use the same ordered groups and rclone copy options
without that flag. Do not use sync or purge. Correct MIME types and
`{CACHE_CONTROL}` are explicit for every upload. Rclone's S3
metadata options are documented at https://rclone.org/s3/#metadata.

Order: dependencies, models, auxiliary metadata, native manifest, simulator catalog,
then the file inventory. Existing canonical model replacements require a coordinated
viewer cutover or a maintenance window so the old viewer cannot consume incompatible
models during the upload. Use the merged backend/frontend revisions in the companion
source-revisions.json. Invalidate affected CDN keys and verify public hashes, MIME,
CORS, and cache behavior after cutover. Do not remove Noesis or legacy fallbacks.

For rollback, restore the replaced keys from rollback/objects with each object's
recorded Content-Type, Cache-Control, Content-Encoding and other serving metadata,
including absent headers. Do not inherit stale local extended attributes;
`rawS3Metadata`, when present, retains the original user metadata separately.
Restore the prior viewer revision and invalidate the affected CDN keys. Newly added
keys can remain unreferenced. No remote rollback or CDN purge has been performed.
''')
    print(json.dumps(dict(prepared=True, uploadFiles=plan['uploadFiles'], rollbackFiles=len(recovery['files']))))
    return plan


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    for name in ['models', 'listing', 'backup', 'metadata']: parser.add_argument('--' + name, type=Path)
    parser.add_argument('--finalize', action='store_true')
    args = parser.parse_args()
    if args.finalize:
        if not args.metadata: parser.error('--finalize requires --metadata')
        finalize(args.output, args.metadata)
    else:
        if not all([args.models, args.listing, args.backup]): parser.error('Preparation requires --models, --listing and --backup')
        prepare(args.models, args.listing, args.backup, args.output)
