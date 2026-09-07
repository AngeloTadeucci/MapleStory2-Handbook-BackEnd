"""Rebuild the Gelo equipment extension from our extracted client resources.

Requires the immutable release-02 base and source paths in gelo-library-plan.json.
No game database, user snapshot, reference assets or publishing are involved.
"""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
from package_simulator import apply_review
from simulator_customization import export


def read(path):
    return json.loads(path.read_text(encoding='utf-8'))


def write(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')


def extend(base, resources, work, output, review=None):
    if work.exists() or output.exists():
        raise ValueError('Choose fresh work and output directories')
    for entry in read(base / 'release-inventory.json')['files']:
        if hashlib.sha256((base / entry['path']).read_bytes()).hexdigest() != entry['sha256']:
            raise ValueError(f'Base release changed: {entry["path"]}')
    here = Path(__file__).parent
    work.mkdir(parents=True)
    def native(*args):
        subprocess.run(['dotnet', 'run', '--project', 'NifToGltf', '--', '--native', *map(str, args)], check=True)
    native('--batch', '--input', resources, '--textures', resources / 'Models/Textures',
           '--manifest', here / 'gelo-library-plan.json', '--output', work / 'models')
    native('--texture-batch', '--input', resources / 'GeloSources/Face', '--output', work / 'faces')
    native('--texture-batch', '--input', resources / 'GeloMissing/Makeup', '--output', work / 'makeup')
    shutil.copytree(base, output)
    for stale in ['appearance-review.json', 'coverage.json', 'release-inventory.json']:
        (output / stale).unlink()
    manifest = read(output / 'native-manifest.json')
    additions = read(work / 'models/native-manifest.json')
    if {a['id'] for a in manifest['assets']} & {a['id'] for a in additions['assets']}:
        raise ValueError('Extension asset duplicates base identity')
    for asset in additions['assets']:
        target = output / asset['uri']
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(work / 'models' / asset['uri'], target)
    manifest['assets'] += additions['assets']
    catalog = read(output / 'simulator-catalog.json')
    new_items = read(here / 'gelo-library-items.json')
    if {(i['itemId'], i['bodyVariant']) for i in catalog['items']} & {(i['itemId'], i['bodyVariant']) for i in new_items}:
        raise ValueError('Extension item duplicates base identity')
    catalog['items'] += new_items
    write(output / 'native-manifest.json', manifest)
    write(output / 'simulator-catalog.json', catalog)
    shutil.copytree(work / 'faces/item_face', output / 'faces/item_face', dirs_exist_ok=True)
    shutil.copytree(work / 'makeup/item_makeup', output / 'makeup/item_makeup')
    shutil.copy2(work / 'models/batch-report.json', output / 'equipment-extension-report.json')
    export(resources / 'SimulatorSources/Xml', output / 'faces', output)
    catalog = read(output / 'simulator-catalog.json')
    if review:
        data = read(review)
        apply_review(output, manifest, catalog, data)
        write(output / 'appearance-review.json', data)
    write(output / 'simulator-catalog.json', catalog)
    write(output / 'coverage.json', {'version': 1, 'models': len(manifest['assets']),
          'catalogEntries': len(catalog['items']), 'availability': dict(Counter(i['availability'] for i in catalog['items'])),
          'excluded': ['effects', 'particles'], 'baseRelease': base.name,
          'limitations': ['Gelo weapons are drawn on baseline body poses, not combat clips.',
                          'The neon sign retains its authored rest pose; independent wing animation is not included.']})
    write(output / 'release-inventory.json', {'version': 1, 'files': [
        {'path': p.relative_to(output).as_posix(), 'bytes': p.stat().st_size,
         'sha256': hashlib.sha256(p.read_bytes()).hexdigest()}
        for p in sorted(output.rglob('*')) if p.is_file()]})
    print(f'Extended library: {len(manifest["assets"])} models and {len(catalog["items"])} item/body entries')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['base', 'resources', 'work', 'output']:
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--review', type=Path)
    args = parser.parse_args()
    extend(args.base, args.resources, args.work, args.output, args.review)
