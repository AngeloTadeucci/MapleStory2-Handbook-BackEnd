"""Audit a read-only R2 listing, database snapshot and local source/export trees.

Does not upload, delete, convert, or claim that an object listing is a backup.
Large generated reports belong under obj/model-migration, outside the publish set.
"""
import argparse
import copy
from collections import Counter, defaultdict
import hashlib
import json
from pathlib import Path, PurePosixPath
import shutil
from kfm_source import read_kfm, referenced_file
from expand_wardrobe import recover_baseline_hair_forms


def read(path):
    return json.loads(path.read_text(encoding='utf-8'))


def write(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2) + '\n', encoding='utf-8')


def sha(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(chunk)
    return digest.hexdigest()


def safe_path(value):
    path = PurePosixPath(value)
    if not value or path.is_absolute() or '..' in path.parts or '\\' in value or ':' in value:
        raise ValueError(f'Unsafe object path: {value}')
    return path


def model_references(database):
    references = defaultdict(list)
    for kind, field in [('items', 'kfms'), ('npcs', 'kfm')]:
        for row in database[kind]:
            names = row[field]
            if kind == 'items' and isinstance(names, str):
                names = json.loads(names)
            if kind == 'npcs':
                names = [names]
            for name in names:
                if name:
                    safe_path(name)
                    references[name.lower()].append({'kind': kind, 'id': row['id'], 'name': name})
    return references


def audit_hair(release, discovery):
    assets = {a['id']: a for a in read(release / 'native-manifest.json')['assets']}
    discovered = {(i['itemId'], i['bodyVariant']): i for i in read(discovery)}
    errors = {e['assetId']: e['reason'] for e in read(release / 'failure-families.json')}
    issues = read(release / 'animation-compatibility.json')
    result = []
    for original in read(release / 'simulator-catalog.json')['items']:
        key = (original['itemId'], original['bodyVariant'])
        if original['slots'] != ['HR'] or key not in discovered or original['availability'] == 'unavailable':
            continue
        lost = {form: ids for form, ids in discovered[key].get('hairForms', {}).items()
                if not original.get('hairForms', {}).get(form)}
        if not lost:
            continue
        entry = copy.deepcopy(original)
        recover_baseline_hair_forms(entry, lost, assets, errors, issues)
        result.append({'itemId': key[0], 'bodyVariant': key[1], 'discoveredForms': lost,
                       'recoveredForms': entry['hairForms'], 'failures': entry.get('hairFormFailures', {}),
                       'candidateEntry': entry})
    return result


def audit(args):
    args.resources = args.resources.resolve()
    args.output.mkdir(parents=True, exist_ok=True)
    remote = read(args.listing)
    database = read(args.database)
    references = model_references(database)
    folders = defaultdict(list)
    for obj in remote:
        key = str(safe_path(obj['Path']))
        folders[key.split('/')[0].lower()].append(obj)
    sources = defaultdict(list)
    source_files, controllers = [], []
    for path in sorted(args.resources.rglob('*')):
        if not path.is_file() or path.suffix.lower() not in {'.nif', '.kf', '.kfm', '.dds'}:
            continue
        # Private artwork is not part of the public conversion inventory.
        if path.relative_to(args.resources).parts[0] == 'GeloSources':
            continue
        relative = path.relative_to(args.resources).as_posix()
        record = {'path': relative, 'bytes': path.stat().st_size, 'sha256': sha(path)}
        source_files.append(record)
        if path.suffix.lower() in {'.nif', '.kfm'}:
            sources[path.stem.lower()].append(record)
        if path.suffix.lower() == '.kfm':
            controller = {'path': relative}
            try:
                data = read_kfm(path.read_bytes())
                controller.update(data)
                controller['modelPath'] = referenced_file(path, data['model'], args.resources).relative_to(args.resources).as_posix()
                controller['missingClips'] = []
                for clip in data['clips']:
                    try:
                        referenced_file(path, clip['file'], args.resources)
                    except (ValueError, OSError) as error:
                        controller['missingClips'].append({'clip': clip, 'error': str(error)})
                controller['duplicateNames'] = [name for name, count in Counter(c['name'] for c in data['clips']).items() if count > 1]
            except (ValueError, OSError) as error:
                controller['error'] = str(error)
            controllers.append(controller)
    assets = read(args.release / 'native-manifest.json')['assets']
    by_source = defaultdict(list)
    for asset in assets:
        by_source[PurePosixPath(asset['input']).stem.lower()].append(asset)
    mapping = []
    for name in sorted(set(references) | {k for k, objects in folders.items() if any(o['Path'].lower().endswith('.gltf') for o in objects)}):
        mapping.append({'model': name, 'databaseReferences': references.get(name, []),
                        'publishedFiles': [o['Path'] for o in folders.get(name, [])],
                        'sources': sources.get(name, []), 'nativeAssets': by_source.get(name, []),
                        'canonicalChoice': None})
    selected = [o for row in mapping for o in folders.get(row['model'], [])]
    # Include the active public simulator and all its auxiliary resources.
    selected += folders.get(args.release.name.lower(), [])
    selected = list({o['Path']: o for o in selected}.values())
    missing_bytes = 0
    for obj in selected:
        local = args.local / obj['Path']
        obj['localStatus'] = 'absent' if not local.is_file() else 'size-match-unverified' if local.stat().st_size == obj['Size'] else 'size-mismatch'
        if obj['localStatus'] != 'size-match-unverified':
            missing_bytes += obj['Size']
    free = shutil.disk_usage(args.output).free
    summary = {'version': 1, 'remoteObjects': len(remote), 'remoteBytes': sum(o['Size'] for o in remote),
               'modelFolders': len(mapping), 'databaseItems': len(database['items']), 'databaseNpcs': len(database['npcs']),
               'selectedObjects': len(selected), 'selectedBytes': sum(o['Size'] for o in selected),
               'minimumDownloadBytes': missing_bytes, 'diskFreeBytes': free, 'reserveBytes': 8 * 1024**3,
               'mirrorFitsWithReserve': missing_bytes + 8 * 1024**3 <= free,
               'sourceFiles': len(source_files), 'controllers': len(controllers),
               'databaseModelsWithoutLocalSource': sum(bool(r['databaseReferences']) and not r['sources'] for r in mapping),
               'sourceNamesWithMultipleNativeAssets': sum(len(v) > 1 for v in by_source.values()),
               'backupVerification': 'not-performed-by-inventory',
               'servingMetadataCapture': 'not-performed-by-inventory',
               'notes': ['Size matches do not establish identical bytes.',
                         'Source coverage searches extracted local files, not unextracted archives.',
                         'Unreferenced non-glTF folders require dependency inspection before declaring complete coverage.',
                         'Canonical choices require explicit variant review. No array-order selection is made.']}
    write(args.output / 'source-inventory.json', source_files)
    write(args.output / 'controllers.json', controllers)
    write(args.output / 'model-mapping.json', mapping)
    write(args.output / 'collisions.json', {k: v for k, v in by_source.items() if len(v) > 1})
    write(args.output / 'recovery-manifest.json', {'version': 1, 'remote': 'r2:handbook-gltfs',
          'listingSha256': sha(args.listing), 'databaseSha256': sha(args.database),
          'verified': False, 'files': selected})
    write(args.output / 'summary.json', summary)
    if args.discovery:
        write(args.output / 'hair-recovery-audit.json', audit_hair(args.release, args.discovery))
    print(json.dumps(summary, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['listing', 'database', 'resources', 'release', 'local', 'output']:
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--discovery', type=Path)
    audit(parser.parse_args())
