"""Copy recovered material render state into an existing local candidate.

Only matching geometry is accepted. Texture and geometry bytes stay unchanged.
"""
import argparse
import hashlib
import json
from pathlib import Path


def update(release, converted):
    def read(path):
        return json.loads(path.read_text())
    manifest = read(release / 'native-manifest.json')
    assets = {a['id']: a for a in manifest['assets']}
    inventory_path = release / 'release-inventory.json'
    inventory = read(inventory_path)
    entries = {e['path']: e for e in inventory['files']}
    replacements = []
    for asset in read(converted / 'native-manifest.json')['assets']:
        old = assets[asset['id']]
        if old['uri'] != asset['uri'] or old['input'] != asset['input']:
            raise ValueError('Source asset identity changed')
        target = release / old['uri']
        data, rebuilt = read(target), read(converted / asset['uri'])
        for key in ['nodes', 'meshes', 'skins', 'buffers', 'accessors', 'bufferViews']:
            if data.get(key) != rebuilt.get(key):
                raise ValueError(f'Geometry changed: {key}')
        if [m['name'] for m in data['materials']] != [m['name'] for m in rebuilt['materials']]:
            raise ValueError('Material identities changed')
        for original, new in zip(data['materials'], rebuilt['materials']):
            original.setdefault('extras', {})['nifRenderState'] = new['extras']['nifRenderState']
        replacements.append((asset['uri'], json.dumps(data, indent=2).encode()))
    for uri, data in replacements:
        (release / uri).write_bytes(data)
        entries[uri].update(bytes=len(data), sha256=hashlib.sha256(data).hexdigest())
    inventory_path.write_text(json.dumps(inventory, indent=2) + '\n')
    print(f'Updated source render state for {len(replacements)} assets')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--release', type=Path, required=True)
    parser.add_argument('--converted', type=Path, required=True)
    args = parser.parse_args()
    update(args.release, args.converted)
