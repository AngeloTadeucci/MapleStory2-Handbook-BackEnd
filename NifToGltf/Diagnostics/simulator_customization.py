"""Export palettes and a bounded face-expression library from our client XML.

Run after simulator_library.py catalog and --texture-batch. No reference-site
assets are used. Default blinks repeat; explicit expressions hold their final
frame. Pose playback and expression playback are separate viewer choices.
"""
import argparse
import copy
import json
from pathlib import Path
import xml.etree.ElementTree as ET


def export(xml, textures, release):
    document = json.loads((release / 'simulator-catalog.json').read_text(encoding='utf-8'))
    # These face IDs explicitly share the same NIF but import different textures.
    for original, alternate in [(10300001, 10300002), (10300003, 10300004)]:
        for item in list(document['items']):
            if item['itemId'] != original:
                continue
            alternate_xml = ET.parse(xml / 'itemmodel/103.xml').getroot().find(f"ItemModel[@id='{alternate}']")
            original_xml = ET.parse(xml / 'itemmodel/103.xml').getroot().find(f"ItemModel[@id='{original}']")
            if [a.attrib for a in alternate_xml.findall('slots/slot/asset')] != [a.attrib for a in original_xml.findall('slots/slot/asset')]:
                raise ValueError('Face geometry alias differs')
            if not any(i['itemId'] == alternate and i['bodyVariant'] == item['bodyVariant'] for i in document['items']):
                alias = copy.deepcopy(item)
                alias['itemId'] = alternate
                document['items'].append(alias)
    palettes = {}
    for palette in ET.parse(xml / 'table/colorpalette.xml').getroot():
        def rgb(value):
            number = int(value, 16)
            return [((number >> shift) & 255) / 255 for shift in (16, 8, 0)]
        palettes[palette.get('id')] = [
            {'id': color.get('colorSN'), 'swatch': '#' + color.get('palette')[-6:],
             'colors': [rgb(color.get(f'ch{i}')) for i in range(3)]}
            for color in palette.findall('color')]
    faces = {}
    models = {i.get('id'): i for f in (xml / 'itemmodel').glob('*.xml') for i in ET.parse(f).getroot()}
    for item in document['items']:
        model = models.get(str(item['itemId']))
        if item['slots'] == ['HR'] and model is not None:
            item['hairScales'] = [[float(v) for v in scale.get('value', '').split(',') if v] for scale in model.findall('slots/slot/scale')]
            transform = model.find('customize/capTransform')
            item['capTransform'] = dict(transform.attrib) if transform is not None else None
        if item['slots'] != ['FA'] or item['availability'] == 'unavailable':
            continue
        imp = ET.parse(xml / f'emotion/item/{item["itemId"]}.xml').getroot().find('import')
        if imp is None:
            continue
        emotions = ET.parse(xml / f'emotion/common/{imp.get("name").lower()}.xml').getroot()
        sequences = {}
        for name in ['default', 'happy', 'angry', 'sad']:
            emotion = emotions.find(f"emotion[@name='{name}']")
            if emotion is None:
                continue
            frames = []
            for frame in emotion.findall("textureani[@target='FA']/texture"):
                def path(attribute):
                    value = frame.get(attribute)
                    return str(Path(value.replace('{0}', imp.get('itemCode')).lower()).with_suffix('.json')).replace('\\', '/') if value else None
                image, mask = path('file'), path('control')
                if not image or not (textures / image).is_file() or (mask and not (textures / mask).is_file()):
                    frames = []
                    break
                frames.append({'image': image, 'mask': mask, 'duration': int(frame.get('delay', '1000'))})
            if frames:
                sequences[name] = {'frames': frames, 'repeat': name == 'default', 'sourceAnimation': emotion.find('anim').get('name') if emotion.find('anim') is not None else None}
        if 'default' not in sequences:
            item['availability'] = 'unavailable'
            item['reason'] = 'Face textures are missing from this library'
        else:
            faces[str(item['itemId'])] = {'code': imp.get('itemCode'), 'sequences': sequences}
    (release / 'simulator-catalog.json').write_text(json.dumps(document, ensure_ascii=False, indent=2), encoding='utf-8')
    (release / 'customization.json').write_text(json.dumps({'version': 1, 'palettes': palettes, 'faces': faces}, indent=2))
    print(f'Exported {len(faces)} face presets and {len(palettes)} palettes')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--xml', type=Path, default=Path('Maple2Storage/Resources/SimulatorSources/Xml'))
    parser.add_argument('--textures', type=Path, required=True)
    parser.add_argument('--release', type=Path, required=True)
    args = parser.parse_args()
    export(args.xml, args.textures, args.release)
