"""Extend an immutable simulator release with the reviewed hair-effect pilot.

Run the effects-hair-plan.json native batch and effect texture batch first.
No database access, private character snapshots, commits or publishing.
"""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import shutil
import xml.etree.ElementTree as ET
from export_hair_effect import export_effect


def read(path):
    return json.loads(path.read_text(encoding='utf-8'))


def write(path, value):
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False), encoding='utf-8')


def build(base, resources, models, textures, output):
    if output.exists():
        raise ValueError('Choose a fresh release output')
    for entry in read(base/'release-inventory.json')['files']:
        if hashlib.sha256((base/entry['path']).read_bytes()).hexdigest() != entry['sha256']:
            raise ValueError(f'Base release changed: {entry["path"]}')
    sources = resources/'SimulatorEffects'
    effect = export_effect(sources/'Models/item/hair/eff_hair_twinkle_a.nif', sources/'Definitions/effect/item/hair/eff_hair_twinkle_a.xml', sources/'Definitions/itemdata/102.xml')
    additions = read(models/'native-manifest.json')['assets']
    expected = {f'{item}-{body}-{form}' for item, body in [(10200121,'male'),(10200122,'male'),(10200123,'female')] for form in ['a','c','d']}
    if {a['id'] for a in additions} != expected:
        raise ValueError('Hair batch is incomplete or includes an unrelated model')
    shutil.copytree(base, output)
    (output/'effects').mkdir()
    write(output/'effects/hair-twinkle-a.json', effect)
    for name in ['hitlight_8-2','gradient_light_02','one_002','alpha_0352']:
        shutil.copy2(textures/f'{name}.png',output/'effects'/f'{name}.png')
    manifest = read(output/'native-manifest.json')
    for asset in additions:
        target=output/asset['uri'];target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copy2(models/asset['uri'],target)
    manifest['assets'].extend(additions)
    write(output/'native-manifest.json',manifest)
    catalog=read(output/'simulator-catalog.json')
    item_models={i.get('id'):i for i in ET.parse(resources/'SimulatorSources/Xml/itemmodel/102.xml').getroot()}
    for item,body,preset in [(10200121,'male','10200103'),(10200122,'male','10200101'),(10200123,'female','10200086')]:
        source=item_models[preset]
        catalog['items'].append(dict(itemId=item,bodyVariant=body,slots=['HR'],
            parts=[dict(assetId=f'{item}-{body}-a',slot='HR')],
            hairForms={f:[f'{item}-{body}-{f}'] for f in ['c','d']},
            hairScales=[[float(v) for v in source.find('slots/slot/scale').get('value').split(',')]],
            customize=source.find('customize').attrib,cutting=[],availability='preview',
            reason='Source hair and twinkle effect preview. Client appearance parity remains unverified.'))
    for item in catalog['items']:
        if item['itemId'] in effect['itemIds']:
            item['cosmeticEffect']='effects/hair-twinkle-a.json'
    write(output/'simulator-catalog.json',catalog)
    coverage=read(output/'coverage.json')
    coverage.update(models=len(manifest['assets']),catalogEntries=len(catalog['items']),
        availability=dict(Counter(i['availability'] for i in catalog['items'])),
        excluded=['badges','unimplemented effect families'],baseRelease=base.name)
    coverage['limitations'].extend(effect['notes'])
    write(output/'coverage.json',coverage)
    write(output/'effect-extension-report.json',dict(version=1,baseRelease=base.name,
        baseInventorySha256=hashlib.sha256((base/'release-inventory.json').read_bytes()).hexdigest(),
        geometryReview='appearance-review.json is the unchanged base geometry review. The three added hairs and all particle appearance are previews.',
        effectSources=effect['sources'],effectHairIds=effect['itemIds'],addedModels=sorted(expected),
        exclusions=['badges'],limitations=effect['notes']))
    inventory=[dict(path=p.relative_to(output).as_posix(),bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in sorted(output.rglob('*')) if p.is_file() and p.name!='release-inventory.json']
    write(output/'release-inventory.json',dict(version=1,files=inventory))
    print(f'Built {len(manifest["assets"])} models, {len(catalog["items"])} entries, {len(inventory)} inventory files')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ['base','resources','models','textures','output']:
        p.add_argument('--'+name,type=Path,required=True)
    a=p.parse_args();build(a.base,a.resources,a.models,a.textures,a.output)
