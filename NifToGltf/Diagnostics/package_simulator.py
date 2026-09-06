"""Assemble a versioned local simulator release candidate, without publishing.

Converted entries remain previews until the explicit visual review file approves
them. The candidate contains its own face, hair-form and background resources.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
from collections import Counter


def apply_review(output, manifest, catalog, review):
    """Approve only explicit item/body identities against the inspected bytes."""
    assets = {a['id']: a for a in manifest['assets']}
    if review['catalogHash'] != hashlib.sha256(json.dumps(catalog, sort_keys=True).encode()).hexdigest():
        raise ValueError('Reviewed catalog metadata changed')
    if any(a['id'] not in review['assetHashes'] for a in manifest['assets'] if not a.get('skeleton')):
        raise ValueError('Review omits a body')
    for identity, expected in review['assetHashes'].items():
        asset = assets.get(identity)
        if asset is None or hashlib.sha256((output / asset['uri']).read_bytes()).hexdigest() != expected:
            raise ValueError(f'Reviewed asset changed or is missing: {identity}')
    for relative, expected in review['fileHashes'].items():
        if hashlib.sha256((output / relative).read_bytes()).hexdigest() != expected:
            raise ValueError(f'Reviewed metadata changed: {relative}')
    entries = {(i['itemId'], i['bodyVariant']): i for i in catalog['items']}
    seen = set()
    for approved in review['items']:
        key = (approved['itemId'], approved['bodyVariant'])
        item = entries.get(key)
        if key in seen or item is None or item['availability'] == 'unavailable':
            raise ValueError(f'Review contains a duplicate or unavailable item: {key}')
        seen.add(key)
        required = [p['assetId'] for p in item['parts']]
        if any(identity not in review['assetHashes'] for identity in required):
            raise ValueError(f'Review omits an item part: {key}')
        if not approved['evidence']:
            raise ValueError(f'Review has no appearance evidence: {key}')
        item['availability'] = 'verified'
        item['reason'] = approved.get('limitation') or 'Reviewed in the supported outfit matrix'


def package(base, hair, faces, backgrounds, output, review_path=None):
    if output.exists():
        raise ValueError('Release destination must not exist')
    shutil.copytree(base, output)
    shutil.copytree(hair, output / 'hair-forms')
    shutil.copytree(faces, output / 'faces')
    shutil.copytree(backgrounds, output / 'backgrounds')
    manifest = json.loads((output / 'native-manifest.json').read_text())
    hair_manifest = json.loads((hair / 'native-manifest.json').read_text())
    catalog = json.loads((output / 'simulator-catalog.json').read_text(encoding='utf-8'))
    for asset in hair_manifest['assets']:
        asset['uri'] = 'hair-forms/' + asset['uri']
        manifest['assets'].append(asset)
    for item in catalog['items']:
        if item['slots'] == ['HR']:
            forms = {}
            for form in ['c', 'd']:
                identity = f'{item["itemId"]}-{item["bodyVariant"]}-{form}'
                if any(a['id'] == identity for a in hair_manifest['assets']):
                    forms[form] = [identity]
            item['hairForms'] = forms
        if item['slots'] == ['CP']:
            # The selected CP sources end in _C; paired client hair _C forms
            # are visually checked with the same cap. Other suffixes fail closed.
            sources = [a['input'].lower() for a in manifest['assets'] if a['id'] in [p['assetId'] for p in item['parts']]]
            if sources and all(p.endswith('_c.nif') for p in sources):
                item['hatHairForm'] = 'c'
            else:
                item['availability'] = 'unavailable'
                item['reason'] = 'Hat fitting has not been established for this model'
    if review_path:
        review = json.loads(review_path.read_text(encoding='utf-8'))
        apply_review(output, manifest, catalog, review)
        (output / 'appearance-review.json').write_text(json.dumps(review, indent=2), encoding='utf-8')
    (output / 'native-manifest.json').write_text(json.dumps(manifest, indent=2))
    (output / 'simulator-catalog.json').write_text(json.dumps(catalog, ensure_ascii=False, indent=2), encoding='utf-8')
    coverage = {
        'version': 1, 'models': len(manifest['assets']),
        'catalogEntries': len(catalog['items']),
        'availability': dict(Counter(i['availability'] for i in catalog['items'])),
        'bodies': {body: dict(Counter(i['availability'] for i in catalog['items'] if i['bodyVariant'] == body))
                   for body in ['male', 'female']},
        'excluded': ['effects', 'particles'],
        'unavailable': [{'itemId': i['itemId'], 'bodyVariant': i['bodyVariant'], 'reason': i['reason']}
                        for i in catalog['items'] if i['availability'] == 'unavailable']
    }
    (output / 'coverage.json').write_text(json.dumps(coverage, indent=2))
    inventory = [{'path': p.relative_to(output).as_posix(), 'bytes': p.stat().st_size,
                  'sha256': hashlib.sha256(p.read_bytes()).hexdigest()}
                 for p in sorted(output.rglob('*')) if p.is_file()]
    (output / 'release-inventory.json').write_text(json.dumps({'version': 1, 'files': inventory}, indent=2))
    print(f'Packaged {len(manifest["assets"])} model assets, {len(catalog["items"])} item/body entries, {len(inventory)} files')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for arg in ['base', 'hair', 'faces', 'backgrounds', 'output']:
        parser.add_argument('--' + arg, type=Path, required=True)
    parser.add_argument('--review', type=Path)
    a = parser.parse_args()
    package(a.base, a.hair, a.faces, a.backgrounds, a.output, a.review)
