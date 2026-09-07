"""Reconcile every client item identity before selecting conversion work. No DB writes.

Inputs are fresh extracts/indexes from one archive revision. The full audit retains
out-of-scope IDs, inactive environments, unused models and exact source records.
The compact discovery catalog never removes an unavailable item or shared preset.
"""
import argparse
from collections import Counter, defaultdict
import copy
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET


WEARABLE = set('HR FA FD LH RH CP MT CL PA GL SH FH EY EA PD RI BE ER OH BH RHLH'.split())


def digest(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, ensure_ascii=False).encode()).hexdigest()


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, separators=(',', ':')), encoding='utf-8')


def tree(element):
    return {'tag': element.tag, 'attributes': dict(element.attrib),
            'children': [tree(child) for child in element]}


def select_environment(item, features, level):
    eligible = []
    for order, env in enumerate(item.findall('environment')):
        feature = env.get('feature')
        version = features.get(feature) if feature else 0
        if version is not None and version <= level and env.get('locale', 'KR') == 'KR':
            eligible.append((version, order, env))
    # Feature environments in this revision are complete overrides, as in the
    # client parser's highest enabled feature selection. Retain all in the audit.
    return max(eligible, key=lambda v: v[:2])[2] if eligible else None


class ArchiveIndex:
    def __init__(self, paths):
        self.paths = {p.lower(): p for p in paths}
        self.qualified = any(p.lower().startswith('item/') for p in paths)
        self.stems = defaultdict(list)
        for p in paths:
            if p.lower().endswith('.nif'):
                self.stems[Path(p).stem.lower()].append(p)

    def resolve(self, name):
        value = name.replace('\\', '/').lower().removeprefix('./')
        if value.startswith('urn:'):
            matches = self.stems[value[4:]]
        else:
            value = value.removeprefix('data/resource/model/' if self.qualified else 'data/resource/model/item/')
            matches = [self.paths[value]] if value in self.paths else []
        return matches[0] if len(matches) == 1 else None, matches


def reconcile(xml, paths):
    features = {e.get('name'): int(e.get('KR', '999'))
                for e in ET.parse(xml / 'table/feature.xml').getroot()}
    level = int(ET.parse(xml / 'table/feature_setting.xml').getroot().find("setting[@type='Live']").get('KR'))
    index = ArchiveIndex(paths)
    models = {}
    provenance = []
    for path in sorted(xml.rglob('*.xml')):
        provenance.append({'path': path.relative_to(xml).as_posix(), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})
    for path in sorted((xml / 'itemmodel').glob('*.xml')):
        for model in ET.parse(path).getroot():
            identity = int(model.get('id'))
            if identity in models:
                raise ValueError(f'Duplicate model {identity}')
            models[identity] = (model, path.relative_to(xml).as_posix())
    names = {int(e.get('id')): e.get('name', '').strip()
             for e in ET.parse(xml / 'string/kr/itemname.xml').getroot()}
    items, excluded, seen, used = [], [], set(), set()
    for path in sorted((xml / 'itemdata').glob('*.xml')):
        for raw in ET.parse(path).getroot():
            identity = int(raw.get('id'))
            if identity in seen:
                raise ValueError(f'Duplicate itemdata {identity}')
            seen.add(identity)
            env = select_environment(raw, features, level)
            audit = {'itemId': identity, 'itemData': path.relative_to(xml).as_posix(),
                     'environments': [tree(e) for e in raw.findall('environment')]}
            if env is None:
                excluded.append({**audit, 'reason': 'No KR Live environment', 'classification': 'inactive'})
                continue
            attributes = {e.tag: dict(e.attrib) for e in env}
            prop, limit, tool = (attributes.get(k, {}) for k in ['property', 'limit', 'tool'])
            category = prop.get('category', '')
            preset = int(tool.get('itemPreset', '0'))
            model, model_path = models.get(preset, (None, None))
            if model is not None:
                used.add(preset)
            slots = sorted({s.get('name') for s in model.findall('slots/slot')}) if model is not None else []
            if category == 'BD':
                excluded.append({**audit, 'reason': 'Badges excluded by scope', 'classification': 'badge'})
                continue
            if not slots and category not in WEARABLE:
                excluded.append({**audit, 'reason': 'No wearable slot or wearable category', 'classification': 'nonwearable', 'category': category, 'presetId': preset})
                continue
            if not slots:
                slots = ['RH', 'LH'] if category in {'BH', 'RHLH'} else [category]
            gender = int(limit.get('genderLimit', '2'))
            if gender not in (0, 1, 2):
                raise ValueError(f'Unknown gender {gender}: {identity}')
            for body, sex in [('male', 0), ('female', 1)]:
                if gender not in (sex, 2):
                    continue
                parts, blockers = [], []
                if model is None:
                    blockers.append(f'Itemmodel preset {preset} is missing')
                else:
                    for slot in model.findall('slots/slot'):
                        for asset in slot.findall('asset'):
                            if asset.get('gender', str(sex)) != str(sex):
                                continue
                            source, matches = index.resolve(asset.get('name', ''))
                            part = {'slot': slot.get('name'), 'source': source, 'declared': asset.get('name'),
                                    'attributes': dict(asset.attrib), 'children': [tree(c) for c in asset],
                                    'selfNode': asset.get('selfnode'), 'targetNode': asset.get('targetnode'),
                                    'replace': asset.get('replace') == '1'}
                            if source is None:
                                blockers.append(f'{"Ambiguous" if matches else "Missing"} Item archive source: {asset.get("name")}')
                                part['matches'] = matches
                            parts.append(part)
                nonvisual = bool(parts) and all(p['source'] and p['source'].lower() in {'empty.nif', 'item/empty.nif'} for p in parts)
                customize = dict(model.find('customize').attrib) if model is not None and model.find('customize') is not None else {}
                customize.update(attributes.get('customize', {}))
                cutting = [m.get('name') for m in model.findall('cutting/mesh') if m.get('gender', str(sex)) == str(sex)] if model is not None else []
                model_record = tree(model) if model is not None else None
                if nonvisual:
                    blockers.append('Nonvisual equipment: client explicitly selects Item/Empty.nif')
                if not parts and 'FD' not in slots:
                    blockers.append(f'No geometry declared for {body}')
                entry = {**audit, 'bodyVariant': body, 'gender': gender, 'slots': slots,
                         'presetId': preset, 'itemModel': model_path, 'modelRecord': model_record,
                         'selectedEnvironment': dict(env.attrib), 'sourceProperties': attributes,
                         'sourceName': names.get(identity) or (model.get('desc', '').strip() if model is not None else ''),
                         'sourceIcon': prop.get('slotIconCustom') if prop.get('slotIcon', '').lower() == 'icon0.png' else prop.get('slotIcon', ''),
                         'isOutfit': int(prop.get('skin', '0')), 'parts': parts, 'customize': customize,
                         'cutting': cutting, 'classification': 'nonvisual' if nonvisual else 'visual',
                         'blockers': blockers, 'conversion': 'not-attempted', 'visualReview': 'unreviewed'}
                # Item identity is deliberately outside the reusable bundle signature.
                canonical = copy.deepcopy(model_record)
                if canonical:
                    canonical['attributes'].pop('id', None)
                    canonical['attributes'].pop('desc', None)
                entry['family'] = digest({'body': body, 'model': canonical, 'customize': customize,
                                          'parts': parts, 'cutting': cutting})
                items.append(entry)
    items.sort(key=lambda i: (i['itemId'], i['bodyVariant']))
    return {'version': 1, 'selection': {'locale': 'KR', 'environment': 'Live', 'featureLevel': level},
            'sourceFiles': provenance, 'archiveIndexHash': digest(paths), 'sourceItemCount': len(seen),
            'items': items, 'excluded': excluded,
            'unreferencedModels': [{'presetId': k, 'source': v[1], 'record': tree(v[0])}
                                   for k, v in models.items() if k not in used]}


def summary(inventory):
    items = inventory['items']
    return {'sourceItems': inventory['sourceItemCount'], 'scopedItems': len({i['itemId'] for i in items}),
            'bodyPairs': len(items), 'families': len({i['family'] for i in items}),
            'classification': dict(Counter(i['classification'] for i in items)),
            'exclusions': dict(Counter(i['classification'] for i in inventory['excluded'])),
            'bySlotBody': {slot: {body: sum(slot in i['slots'] and i['bodyVariant'] == body for i in items)
                                  for body in ['male', 'female']} for slot in sorted({s for i in items for s in i['slots']})},
            'blockers': dict(Counter(reason for i in items for reason in i['blockers'])),
            'unnamedPairs': sum(not i['sourceName'] for i in items)}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--xml', type=Path, required=True)
    parser.add_argument('--index', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    inventory = reconcile(args.xml, json.loads(args.index.read_text()))
    write(args.output / 'wardrobe-inventory.json', inventory)
    report = summary(inventory)
    write(args.output / 'coverage-by-slot-body.json', report)
    print(json.dumps({k: v for k, v in report.items() if k != 'blockers'}, indent=2))
