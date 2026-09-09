"""Create a fresh simulator release with converted character animations.

Run from the backend root. Convert character-animation-plan.json first, using
the matching Character.m2d clips extracted into Resources/SimulatorAnimations.
The base release and its equipment are preserved. Nothing is published.
"""
import argparse
from copy import deepcopy
import hashlib
import json
from pathlib import Path
import shutil


def read(path):
    return json.loads(path.read_text(encoding='utf-8'))


def write(path, value):
    path.write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')


def preserve_body_appearance(document, previous):
    for key in ['nodes', 'meshes', 'skins', 'textures']:
        if document[key] != previous[key]:
            raise ValueError(f'Animation rebuild changed body {key}')
    materials = deepcopy(document['materials'])
    if len(materials) != len(previous['materials']):
        raise ValueError('Animation rebuild changed body materials')
    for material, original in zip(materials, previous['materials']):
        # New converter metadata is applied only through separately reviewed
        # render-state updates. An animation extension keeps the base appearance.
        if 'nifRenderState' not in original.get('extras', {}):
            material.get('extras', {}).pop('nifRenderState', None)
    if materials != previous['materials']:
        raise ValueError('Animation rebuild changed body materials')
    if [i['name'] for i in document['images']] != [i['name'] for i in previous['images']]:
        raise ValueError('Animation rebuild changed body images')
    document['materials'] = deepcopy(previous['materials'])
    document['images'] = deepcopy(previous['images'])


def package(base, converted, output):
    if output.exists():
        raise ValueError('Choose a fresh output directory')
    plan = read(Path(__file__).with_name('character-animation-plan.json'))
    manifest = read(base / 'native-manifest.json')
    additions = read(converted / 'native-manifest.json')['assets']
    expected = {model['id']: model for model in plan['models']}
    if {asset['id'] for asset in additions} != set(expected):
        raise ValueError('Expected exactly the two player bodies')
    documents = {}
    for asset in additions:
        model = expected[asset['id']]
        previous = next(a for a in manifest['assets'] if a['id'] == asset['id'])
        document = read(converted / asset['uri'])
        if (asset['uri'] != previous['uri'] or asset['clips'] != model['clips']
                or [a['name'] for a in document['animations']] != model['clips']
                or not set(previous['clips']).issubset(asset['clips'])):
            raise ValueError('Body identity or animation selection mismatch')
        preserve_body_appearance(document, read(base / asset['uri']))
        documents[asset['id']] = document
    # Verify the base before using it to assemble another release.
    for entry in read(base / 'release-inventory.json')['files']:
        if hashlib.sha256((base / entry['path']).read_bytes()).hexdigest() != entry['sha256']:
            raise ValueError(f'Base release changed: {entry["path"]}')
    shutil.copytree(base, output)
    for asset in additions:
        # Preserve the reviewed body and embedded textures. Only animations and
        # their buffer/accessor data may change in this extension.
        write(output / asset['uri'], documents[asset['id']])
        index = next(i for i, old in enumerate(manifest['assets']) if old['id'] == asset['id'])
        manifest['assets'][index] = asset
    write(output / 'native-manifest.json', manifest)
    shutil.copy2(converted / 'batch-report.json', output / 'character-animation-report.json')
    write(output / 'character-animation-provenance.json', {
        'version': 1, 'baseRelease': base.name,
        'plan': 'NifToGltf/Diagnostics/character-animation-plan.json',
        'bodies': [{'id': a['id'], 'clips': a['clips'],
                    'sha256': hashlib.sha256((output / a['uri']).read_bytes()).hexdigest()}
                   for a in additions]
    })
    write(output / 'release-inventory.json', {'version': 1, 'files': [
        {'path': p.relative_to(output).as_posix(), 'bytes': p.stat().st_size,
         'sha256': hashlib.sha256(p.read_bytes()).hexdigest()}
        for p in sorted(output.rglob('*')) if p.is_file() and p.name != 'release-inventory.json'
    ]})
    print(f'Packaged {len(additions)} bodies with {len(additions[0]["clips"])} animations each at {output}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['base', 'converted', 'output']:
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    package(args.base, args.converted, args.output)
