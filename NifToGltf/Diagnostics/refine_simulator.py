"""Rebuild the motion/compatibility release from an immutable local base.

Source extracts: SimulatorMotion/Item (11850281 NIF/KF), SimulatorMotion/Body
(the eight explicitly selected player KFs per body). Never publishes or reads DBs.
"""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
from package_simulator import apply_review


def read(path):
    return json.loads(path.read_text(encoding='utf-8'))


def write(path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8')


def refine(base, resources, work, output, review=None):
    if output.exists() or work.exists():
        raise ValueError('Choose fresh output and work directories')
    for entry in read(base / 'release-inventory.json')['files']:
        if hashlib.sha256((base / entry['path']).read_bytes()).hexdigest() != entry['sha256']:
            raise ValueError(f'Base changed: {entry["path"]}')
    work.mkdir(parents=True)
    # Re-export all existing models so material metadata comes from the same
    # converter/source as geometry. Explicit extension plans retain aliases/hands.
    source_manifest = read(base / 'native-manifest.json')
    models = {}
    for asset in source_manifest['assets']:
        model = {k: asset[k] for k in ['id', 'input', 'skeleton', 'attach', 'slot', 'bodyVariant', 'itemId'] if asset.get(k) is not None}
        model['output'] = asset['uri']
        if model.get('itemId'):
            model['itemModel'] = f'SimulatorSources/Xml/itemmodel/{str(model["itemId"])[:3]}.xml'
        if str(model.get('itemId', '')).startswith('155'):
            model['drawn'] = True
        models[asset['id']] = model
    for plan in ['gelo-library-plan.json', 'motion-library-plan.json']:
        for model in read(Path(__file__).with_name(plan))['models']:
            models[model['id']] = model
    write(work / 'plan.json', {'version': 1, 'models': list(models.values())})
    converted = work / 'models'
    subprocess.run(['dotnet', 'run', '--project', 'NifToGltf', '--', '--native', '--batch',
                    '--input', str(resources), '--textures', str(resources / 'Models/Textures'),
                    '--manifest', str(work / 'plan.json'),
                    '--output', str(converted)], check=True)
    shutil.copytree(base, output)
    for name in ['appearance-review.json', 'release-inventory.json', 'coverage.json']:
        (output / name).unlink()
    manifest = read(output / 'native-manifest.json')
    replacements = {a['id']: a for a in read(converted / 'native-manifest.json')['assets']}
    for i, asset in enumerate(manifest['assets']):
        if asset['id'] in replacements:
            new = replacements.pop(asset['id'])
            if new['uri'] != asset['uri']:
                raise ValueError('Replacement URI changed')
            shutil.copy2(converted / new['uri'], output / new['uri'])
            manifest['assets'][i] = new
    for asset in replacements.values():
        target = output / asset['uri']
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(converted / asset['uri'], target)
        manifest['assets'].append(asset)
    catalog = read(output / 'simulator-catalog.json')
    for item in catalog['items']:
        if item['itemId'] == 11800001:
            item['availability'] = 'preview'
            item['reason'] = 'Private wing joints retained; awaiting appearance review'
        if item['itemId'] == 11820024:
            item['reason'] = 'Source heart and wing animation; effects excluded'
        if item['itemId'] == 13400306:
            item['stowedParts'] = [f'13400306-{item["bodyVariant"]}-stowed']
            item['reason'] = 'Drawn and XML back placement; paired back overlap remains unverified'
    write(output / 'native-manifest.json', manifest)
    if review:
        data = read(review)
        apply_review(output, manifest, catalog, data)
        write(output / 'appearance-review.json', data)
    write(output / 'simulator-catalog.json', catalog)
    shutil.copy2(converted / 'batch-report.json', output / 'motion-report.json')
    write(output / 'coverage.json', {'version': 1, 'models': len(manifest['assets']),
          'catalogEntries': len(catalog['items']),
          'availability': dict(Counter(i['availability'] for i in catalog['items'])),
          'excluded': ['effects', 'particles'], 'baseRelease': base.name,
          'limitations': ['Paired star back placement uses the same XML anchor; client parity is unresolved.',
                          'Shader approximation does not establish full client rendering parity.']})
    write(output / 'release-inventory.json', {'version': 1, 'files': [
        {'path': p.relative_to(output).as_posix(), 'bytes': p.stat().st_size,
         'sha256': hashlib.sha256(p.read_bytes()).hexdigest()}
        for p in sorted(output.rglob('*')) if p.is_file()]})
    print(f'Refined library at {output}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['base', 'resources', 'work', 'output']:
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--review', type=Path)
    args = parser.parse_args()
    refine(args.base, args.resources, args.work, args.output, args.review)
