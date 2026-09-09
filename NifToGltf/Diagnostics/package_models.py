"""Stage canonical native model folders locally. Never modifies inputs or publishes.

Names and standalone choices are deterministic. Ambiguous attachment variants
remain explicit and have no default; they are reported for review.
"""
import argparse
from collections import Counter, defaultdict
import copy
import hashlib
import json
import os
from pathlib import Path
import re
from urllib.parse import unquote, urlsplit
from migration_inventory import read, write, safe_path, sha, audit_hair, model_references


def slug(value):
    result = re.sub('[^a-z0-9_-]+', '-', value.lower()).strip('-')
    if not result:
        raise ValueError('Empty model name')
    return result


def layout(assets, primary_ids=None):
    if len({asset['id'].lower() for asset in assets}) != len(assets):
        raise ValueError('Duplicate native asset identity')
    groups = defaultdict(list)
    for asset in assets:
        groups[slug(asset.get('model') or Path(asset['input']).stem)].append(copy.deepcopy(asset))
    output, choices = [], []
    for model, variants in sorted(groups.items()):
        exact = [a for a in variants if a['id'].lower() == model and not a.get('attachment') and not a.get('attach')]
        candidates = exact or [a for a in variants if primary_ids and a['id'] in primary_ids] or variants
        bodies = defaultdict(list)
        for asset in candidates:
            bodies[asset.get('bodyVariant') or 'standalone'].append(asset)
        # A unique authored body is canonical. For a simple unisex pair, use
        # male explicitly, retaining female beside it. More placements need review.
        canonical = candidates[0] if len(candidates) == 1 else (
            bodies['male'][0] if set(bodies) == {'male', 'female'} and all(len(v) == 1 for v in bodies.values()) else None)
        all_bodies = defaultdict(list)
        for asset in variants:
            all_bodies[asset.get('bodyVariant') or 'standalone'].append(asset)
        for asset in sorted(variants, key=lambda a: a['id']):
            body = slug(asset.get('bodyVariant') or 'standalone')
            suffix = '' if asset is canonical else '-' + body
            if asset is not canonical and len(all_bodies[asset.get('bodyVariant') or 'standalone']) > 1:
                suffix += '-' + slug(asset.get('slot') or 'model') + '-' + hashlib.sha256(asset['id'].encode()).hexdigest()[:12]
            asset.update(model=model, standalone=asset is canonical,
                         uri=f'{model}/{model}{suffix}.gltf')
            if asset['id'] in {'wardrobe-fbb6ec3687b62815b3fb4de0', 'wardrobe-f5ce58f738334c90c520c2d7'}:
                asset['compatibility'] = {'movableHatPlacement': True}
            output.append(asset)
        choices.append({'model': model, 'defaultAssetId': canonical['id'] if canonical else None,
                        'variants': [a['id'] for a in variants],
                        'reason': 'exact-model-identity' if canonical and exact else 'unique-primary-catalog-variant' if len(candidates) == 1 else 'explicit-male-default-for-body-pair' if canonical else 'attachment-variants-require-review'})
    paths = [a['uri'] for a in output]
    if len(paths) != len(set(paths)):
        raise ValueError('Canonical path collision')
    return output, choices


def link(source, destination):
    if destination.exists():
        raise ValueError('Candidate path already exists: ' + str(destination))
    destination.parent.mkdir(parents=True, exist_ok=True)
    os.link(source, destination)


def retain_legacy(mirror, output, names):
    """Retain published fallbacks and per-animation files until acceptance passes."""
    mirror = mirror.resolve()
    private = re.compile(r'(character-previews|simulator-release-|.*preview|gelo)', re.I)
    # Published-only models also have consumers. A database snapshot alone is
    # not a complete list of legacy folders that must survive migration.
    names = set(names) | {folder.name for folder in mirror.iterdir()
                         if folder.is_dir() and not private.match(folder.name) and any(folder.glob('*.gltf'))}
    pending = []
    missing = []
    for name in sorted(set(names)):
        relative = safe_path(name)
        if len(relative.parts) != 1 or private.match(name):
            raise ValueError('Invalid legacy model folder: ' + name)
        folder = mirror / relative
        if not folder.is_dir():
            missing.append(name)
            continue
        pending.extend(path for path in folder.rglob('*') if path.is_file())
    visited, retained = set(), []
    while pending:
        source = pending.pop().resolve()
        if not source.is_relative_to(mirror):
            raise ValueError('Legacy dependency escapes mirror')
        relative = source.relative_to(mirror).as_posix()
        if relative in visited:
            continue
        visited.add(relative)
        target = output / relative
        if target.exists():
            continue
        if source.suffix.lower() == '.gltf':
            document = read(source)
            for kind in ['buffers', 'images']:
                for resource in document.get(kind, []):
                    uri = resource.get('uri', '')
                    if not uri or uri.startswith('data:'):
                        continue
                    parsed = urlsplit(uri)
                    if parsed.scheme or parsed.netloc or parsed.query or parsed.fragment:
                        raise ValueError('Legacy dependency needs explicit mapping: ' + uri)
                    dependency = (source.parent / unquote(parsed.path)).resolve()
                    if not dependency.is_relative_to(mirror) or not dependency.is_file():
                        raise ValueError('Missing or unsafe legacy dependency: ' + uri)
                    pending.append(dependency)
        link(source, target)
        retained.append(relative)
    return {'files': sorted(retained), 'missingFolders': missing, 'status': 'retained-pending-native-acceptance'}


def package(release, output, discovery, npc=None, legacy=None, database=None):
    if output.exists():
        raise ValueError('Choose a fresh candidate directory')
    manifest = read(release / 'native-manifest.json')
    sources = {a['id']: release / a['uri'] for a in manifest['assets']}
    if npc:
        for asset in read(npc / 'native-manifest.json')['assets']:
            if asset['id'] in sources:
                raise ValueError('Duplicate NPC asset identity')
            asset = dict(asset, model=asset['id'])
            manifest['assets'].append(asset)
            sources[asset['id']] = npc / asset['uri']
    catalog = read(release / 'simulator-catalog.json')
    primary_ids = {p['assetId'] for i in catalog['items'] if i['availability'] != 'unavailable' for p in i['parts']}
    assets, choices = layout(manifest['assets'], primary_ids)
    faces = read(release / 'customization.json')['faces']
    for asset in assets:
        preset = ('10300001' if asset.get('bodyVariant') == 'male' else '10300003') if asset['id'] in {'f_body', 'm_body'} else asset.get('itemId') if asset.get('slot') == 'FA' else None
        if preset and preset in faces:
            asset.update(facePreset=preset, customizationUri='customization.json')
    output.mkdir(parents=True)
    source_uris = {a['uri'] for a in read(release / 'native-manifest.json')['assets']}
    # Public auxiliary resources retain their paths relative to customization.json.
    for record in read(release / 'release-inventory.json')['files']:
        relative = str(safe_path(record['path']))
        if relative in source_uris or '/' not in relative:
            continue
        if relative.endswith('.gltf'):
            raise ValueError('Unreferenced model in release inventory: ' + relative)
        link(release / relative, output / relative)
    for asset in assets:
        source = sources[asset['id']]
        gltf = read(source)
        external = [r['uri'] for kind in ['buffers', 'images'] for r in gltf.get(kind, [])
                    if r.get('uri') and not r['uri'].startswith('data:')]
        if external:
            raise ValueError('Relocation requires explicit dependency mapping: ' + asset['id'])
        asset['clipMetadata'] = [{'name': clip['name'], 'duration': max(
            (gltf['accessors'][s['input']].get('max', [0])[0] for s in clip['samplers']), default=0)}
            for clip in gltf.get('animations', [])]
        link(source, output / asset['uri'])
    manifest['assets'] = assets
    catalog = read(release / 'simulator-catalog.json')
    recovered = {(r['itemId'], r['bodyVariant']): r['candidateEntry'] for r in audit_hair(release, discovery)}
    catalog['items'] = [recovered.get((i['itemId'], i['bodyVariant']), i) for i in catalog['items']]
    write(output / 'native-manifest.json', manifest)
    write(output / 'simulator-catalog.json', catalog)
    write(output / 'model-variants.json', {'version': 1, 'models': choices})
    link(release / 'customization.json', output / 'customization.json')
    coverage = read(release / 'coverage.json')
    coverage['availability'] = dict(Counter(item['availability'] for item in catalog['items']))
    by_slot_body = defaultdict(lambda: defaultdict(Counter))
    for item in catalog['items']:
        for slot in item['slots']:
            by_slot_body[slot][item['bodyVariant']][item['availability']] += 1
    coverage['bySlotBody'] = by_slot_body
    coverage['nativeAssets'] = len(assets)
    write(output / 'coverage.json', coverage)
    if legacy:
        names = set(model_references(read(database))) | {choice['model'] for choice in choices}
        write(output / 'legacy-retention.json', retain_legacy(legacy, output, names))
    files = [{'path': p.relative_to(output).as_posix(), 'bytes': p.stat().st_size, 'sha256': sha(p)}
             for p in sorted(output.rglob('*')) if p.is_file()]
    write(output / 'model-files.json', {'version': 1, 'cacheControl': 'public, max-age=0, must-revalidate', 'files': files})
    print(json.dumps({'assets': len(assets), 'ambiguousDefaults': sum(c['defaultAssetId'] is None for c in choices), 'files': len(files)}))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['release', 'output', 'discovery']:
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--npc', type=Path)
    parser.add_argument('--legacy', type=Path, help='Verified published mirror, retained until native acceptance')
    parser.add_argument('--database', type=Path, help='Read-only reference snapshot for legacy retention')
    args = parser.parse_args()
    if bool(args.legacy) != bool(args.database):
        parser.error('--legacy and --database must be supplied together')
    package(args.release, args.output, args.discovery, args.npc, args.legacy, args.database)
