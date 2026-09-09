"""Download R2 recovery objects and rehearse restoration on an isolated local copy.

The remote is a fixed read-only source. No upload or remote restore command exists.
Full collection backups require the complete inventory, including dependencies.
"""
import argparse
import base64
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
from migration_inventory import read, write, safe_path, sha


REMOTE = 'r2:handbook-gltfs/'


def stat(key):
    return json.loads(subprocess.check_output(
        ['rclone', 'lsjson', REMOTE + key, '--stat', '--metadata', '--hash'], text=True))


def local_path(root, key):
    path = root / str(safe_path(key))
    if not path.resolve().is_relative_to(root.resolve()):
        raise ValueError('Recovery path escapes root through a symlink')
    return path


def verify(root, records):
    for record in records:
        path = local_path(root, record['path'])
        if path.is_symlink() or not path.is_file() or path.stat().st_size != record['bytes'] or sha(path) != record['sha256']:
            raise ValueError('Recovery bytes changed: ' + record['path'])


def backup(inventory, output):
    records = read(inventory)['files']
    keys = [str(safe_path(r['Path'])) for r in records]
    if len(set(keys)) != len(keys):
        raise ValueError('Duplicate recovery keys')
    output.mkdir(parents=True, exist_ok=True)
    manifest_path = output / 'backup.json'
    manifest = read(manifest_path) if manifest_path.exists() else {
        'version': 1, 'remote': REMOTE, 'inventorySha256': sha(inventory), 'complete': False, 'files': []}
    if manifest['inventorySha256'] != sha(inventory):
        raise ValueError('Recovery inventory changed; use a fresh directory')
    objects = output / 'objects'
    verify(objects, manifest['files'])
    done = {r['path'] for r in manifest['files']}
    needed = sum(r['Size'] for r in records if r['Path'] not in done)
    if shutil.disk_usage(output).free < needed + 8 * 1024**3:
        raise ValueError(f'Backup needs {needed} bytes plus an 8 GiB reserve')
    for key in keys:
        if key in done:
            continue
        before = stat(key)
        path = local_path(objects, key)
        path.parent.mkdir(parents=True, exist_ok=True)
        temporary = path.with_name(path.name + '.partial')
        if path.exists() or temporary.exists():
            raise ValueError('Untracked recovery file exists: ' + key)
        subprocess.run(['rclone', 'copyto', REMOTE + key, str(temporary)], check=True)
        after = stat(key)
        if before != after or temporary.stat().st_size != before['Size']:
            raise ValueError('Remote object changed during download: ' + key)
        expected_md5 = before.get('Hashes', {}).get('md5')
        if not expected_md5:
            raise ValueError('Remote object has no verifiable MD5: ' + key)
        digest = hashlib.md5()
        with temporary.open('rb') as stream:
            for chunk in iter(lambda: stream.read(1024 * 1024), b''):
                digest.update(chunk)
        if digest.hexdigest() != expected_md5:
            raise ValueError('Remote checksum mismatch: ' + key)
        temporary.rename(path)
        manifest['files'].append({'path': key, 'bytes': before['Size'], 'sha256': sha(path), 'remote': before})
        write(manifest_path, manifest)
    manifest['complete'] = len(manifest['files']) == len(keys)
    write(manifest_path, manifest)
    print(json.dumps({'verifiedObjects': len(manifest['files']), 'completeForInventory': manifest['complete']}))


def restore(backup_root, output):
    manifest = read(backup_root / 'backup.json')
    if not manifest['complete']:
        raise ValueError('Backup is incomplete')
    if output.exists():
        raise ValueError('Restore destination must not exist')
    verify(backup_root / 'objects', manifest['files'])
    for record in manifest['files']:
        target = local_path(output, record['path'])
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(local_path(backup_root / 'objects', record['path']), target)
        # Deduplicated mirrors keep per-object metadata in the manifest,
        # since hard links share filesystem metadata with their content peer.
        if 'xattrsBase64' in record:
            for name in os.listxattr(target):
                os.removexattr(target, name)
            for name, value in record['xattrsBase64'].items():
                os.setxattr(target, name, base64.b64decode(value, validate=True))
            actual = {name: base64.b64encode(os.getxattr(target, name)).decode()
                      for name in os.listxattr(target)}
            if actual != record['xattrsBase64']:
                raise ValueError('Restored metadata mismatch: ' + record['path'])
        if 'mtimeNs' in record:
            os.utime(target, ns=(target.stat().st_atime_ns, record['mtimeNs']))
    verify(output, manifest['files'])
    print(json.dumps({'restoredAndVerifiedObjects': len(manifest['files']), 'remoteWrites': 0}))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['backup', 'restore'])
    parser.add_argument('input', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    (backup if args.command == 'backup' else restore)(args.input, args.output)
