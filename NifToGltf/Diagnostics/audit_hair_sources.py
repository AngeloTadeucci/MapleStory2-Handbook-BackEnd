"""Audit the local hair catalog, explicit XML/KFM references and raw material bindings.

Read-only inputs. No guessed texture or model substitutions. Run from the backend root.
"""
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET
from inspect_nif import inspect, read_document, object_net
from scan_nif import Reader
from kfm_source import read_kfm


def texture_bindings(path):
    strings, blocks, _ = read_document(path)
    result = []
    for index, (kind, payload) in enumerate(blocks):
        if kind != 'NiTexturingProperty':
            continue
        r = Reader(payload)
        object_net(r, strings)
        r.number('H')
        def descriptor():
            source = r.number('i')
            r.array('H', 2)
            if r.number('B'):
                r.take(28)
            return source
        bindings = {}
        for slot in range(r.number()):
            if not r.number('B'):
                continue
            bindings[f'slot{slot}'] = descriptor()
            if slot == 5:
                r.take(24)
            if slot == 7:
                r.take(4)
        for _ in range(r.number()):
            if r.number('B'):
                source = descriptor()
                bindings[f'shader{r.number()}'] = source
        r.finish()
        textures = {b['id']: b.get('filename') for b in inspect(path)['blocks'] if b['type'] == 'NiSourceTexture'}
        result.append({'block': index, 'bindings': {slot: textures.get(source) for slot, source in bindings.items()}})
    return result


def audit():
    root = Path('NifToGltf/obj')
    xml = root / 'wardrobe/xml'
    source = root / 'hair-investigation/source/0/02'
    model = {int(e.get('id')): e for e in ET.parse(xml / 'itemmodel/102.xml').getroot()}
    data = {int(e.get('id')): e for e in ET.parse(xml / 'itemdata/102.xml').getroot()}
    catalog = json.loads(Path('../MapleStory2-Handbook/static/gltf/simulator-release-14/simulator-catalog.json').read_text())
    archive = json.loads((root / 'hair-investigation/curly/archive-recheck.json').read_text())
    rows = []
    for item in catalog['items']:
        if 'HR' not in item['slots'] or item['availability'] != 'unavailable':
            continue
        id = item['itemId']
        state, category = 'preview', 'placement'
        detail = 'Existing geometry with source-authored placement. Source physics is not simulated.'
        if id in [10200010, 10200011, 10200012]:
            category = 'alias'
            detail = 'Separate attachments use explicit second-tail KFM model references. Browser motion is optional for Sassy only.'
        elif id == 10200070:
            category = 'animation target'
            detail = 'Separate export succeeds with only three absent posed Point01 NonAccum channels unbound. Four-bone physics is omitted.'
        elif id == 10200031:
            state, category = 'unavailable', 'material'
            detail = 'Both raw tail material properties lack shader1 direction binding. No substitute assigned.'
        elif id == 10200159:
            state, category = 'unavailable', 'source'
            detail = 'A/C/D KFMs each name an absent matching KF. No alternate clip inferred from EMPTY_SEQUENCE name.'
        elif id in [10200246, 10200260, 10200262, 10200263]:
            state, category = 'unavailable', 'source'
            detail = 'Explicit self itemPreset and itemmodel entry exist, but requested NIF is absent in the local Item archive. Development label is not proof of usability.'
        rows.append({'itemId': id, 'name': item.get('sourceName'), 'body': item['bodyVariant'], 'status': state, 'category': category, 'detail': detail,
                     'itemPresets': [el.get('itemPreset') for el in data[id].iter('tool')],
                     'models': [el.get('name') for el in model[id].iter('asset')]})
    materials = {p.name: texture_bindings(p) for p in source.glob('10200031*ironroll*.nif')}
    kfms = {p.name: read_kfm(p.read_bytes()) for p in source.glob('10200159*.kfm')}
    return {'version': 1, 'scope': 'Existing release-14 unavailable HR entries; exact local Item archive reopened for named missing sources. No claim about other client versions.',
            'originalUnavailableCount': len(rows), 'remainingUnavailableCount': sum(r['status'] == 'unavailable' for r in rows), 'items': rows,
            'curledTailMaterials': materials, 'starCandyKfms': kfms, 'archiveMatches': archive,
            'sourceHashes': {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in [xml/'itemmodel/102.xml', xml/'itemdata/102.xml']}}

if __name__ == '__main__':
    print(json.dumps(audit(), indent=2))
