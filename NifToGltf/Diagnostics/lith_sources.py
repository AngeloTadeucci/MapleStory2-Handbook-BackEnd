"""Build the pipeline's itemdata/itemmodel source layout from LithMS2-XML.

LithMS2-XML is the game data the Handbook database and the Lith server both use.
It stores one item per file, and each environment carries the item's own slots,
customize and cutting instead of pointing into a combined itemmodel file. This
writes the layout wardrobe_inventory.py and the converter's ItemModelAttachment
reader expect, so the rest of the pipeline runs unchanged:

- itemdata/<prefix>.xml: one <item> per id holding only its resolved environment.
- itemmodel/<prefix>.xml: one <ItemModel> per id with that environment's slots,
  customize and cutting. It is keyed by item id rather than itemPreset because
  items sharing a preset do not always share slots. The original preset is kept
  as lithItemPreset on the itemdata tool element.

An environment whose only slot has an empty name is not equipment: furniture,
pets and trophies carry their model that way. KMS2 expresses the same thing as
itemPreset 0 with no itemmodel entry, so those items get exactly that.

The environment is resolved exactly as Maple2.File.Parser's FeatureLocaleFilter
does for the server, so the build sees the item the client and server see.

Customize fields are nested per slot here, as <HR scale>, <FD translation rotation
scale> and <CP xrotation attach><transform/></CP>. KMS2 flattens them onto
<customize> and names the cap offset <capTransform>. Readers such as
hat_placement_metadata.py and simulator_customization.py expect the KMS2 names,
so they are added alongside the originals. The mapping was measured on the 6,264
models both sources share: HR/CP/FD scale agree 100/100/98.9 percent, FD
translation 95.8, FD rotation 98.9, CP xrotation 94.4, CP attach 95.5, and the
CP transform 89.4 and 95.7 for position and rotation. KMS2 omits a flag that is
zero, and Lith has zero wherever KMS2 omits one, so zeros are not written.
"""
import argparse
import hashlib
import json
import shutil
from collections import defaultdict
from pathlib import Path
import xml.etree.ElementTree as ET


MODEL_CHILDREN = ('slots', 'customize', 'cutting')
ANIMATION_URN = 'urn:gamebryo-animation:urn:'


def enabled_features(xml, locale, env):
    setting = next(s for s in ET.parse(xml / 'table/feature_setting.xml').getroot() if s.get('type') == env)
    live = int(setting.get(locale))
    return {f.get('name'): int(f.get(locale)) for f in ET.parse(xml / 'table/feature.xml').getroot()
            if int(f.get(locale, '999')) <= live}


def resolve_environment(environments, features, locale):
    eligible = [e for e in environments
                if (not e.get('feature') or e.get('feature') in features)
                and (not e.get('locale') or e.get('locale').lower() == locale.lower())]
    matched = [e for e in eligible if (e.get('locale') or '').lower() == locale.lower()]
    candidates = matched or [e for e in eligible if not e.get('locale')]
    result, best = None, -1
    for entry in candidates:
        feature = entry.get('feature') or ''
        # Mirrors the parser: an unfeatured entry only wins while no featured one has,
        # and a later unfeatured entry replaces an earlier one.
        if not feature.strip():
            if best == -1:
                result = entry
            continue
        version = features[feature]
        if version > best or (version == best and feature > (result.get('feature') or '')):
            result, best = entry, version
    return result


def has_equipment(environment):
    return any(slot.get('name') for slot in environment.findall('slots/slot'))


def flatten_customize(customize, slots):
    def value(path, attribute):
        element = customize.find(path)
        return element.get(attribute, '0') if element is not None else '0'
    flags = {
        'scale': next((value(slot, 'scale') for slot in ('HR', 'CP', 'FD') if slot in slots), '0'),
        'rotation': value('FD', 'rotation') if 'FD' in slots else value('CP', 'xrotation') if 'CP' in slots else '0',
        'translation': value('FD', 'translation'),
        'capAttach': value('CP', 'attach'),
    }
    for attribute, flag in flags.items():
        if flag not in ('0', '') and attribute not in customize.attrib:
            customize.set(attribute, flag)
    transform = customize.find('CP/transform')
    if transform is not None and transform.get('position') and customize.find('capTransform') is None:
        ET.SubElement(customize, 'capTransform', position=transform.get('position'),
                      rotation=transform.get('rotation', '0,0,0'))


def item_model(identity, environment, name):
    model = ET.Element('ItemModel', id=str(identity), desc=name)
    for tag in MODEL_CHILDREN:
        child = environment.find(tag)
        if child is None:
            continue
        child = ET.fromstring(ET.tostring(child))
        if tag == 'slots':
            for slot in [s for s in child if not s.get('name')]:
                child.remove(slot)
        if tag == 'customize':
            flatten_customize(child, {slot.get('name') for slot in environment.findall('slots/slot')})
        for element in child.iter():
            # gender="2" is unisex in this XML. The KMS2 layout omits the attribute instead,
            # and both wardrobe_inventory and ItemModelAttachment read absence as unisex.
            if element.get('gender') == '2':
                del element.attrib['gender']
            # The same animated asset is urn:gamebryo-animation:urn:X here and urn:X in KMS2.
            # Neither the inventory's archive index nor the converter reads the long form.
            # Some assets repeat the prefix several times; KMS2 names the same asset once.
            declared = element.get('name', '') if element.tag == 'asset' else ''
            if declared.startswith(ANIMATION_URN):
                while declared.startswith(ANIMATION_URN):
                    declared = 'urn:' + declared[len(ANIMATION_URN):]
                element.set('name', declared)
        model.append(child)
    return model


def item_data(identity, environment):
    item = ET.Element('item', id=str(identity))
    if environment is None:
        return item
    resolved = ET.SubElement(item, 'environment')
    for child in environment:
        if child.tag == 'slots':
            continue
        child = ET.fromstring(ET.tostring(child))
        if child.tag == 'tool':
            child.set('lithItemPreset', child.get('itemPreset', '0'))
            child.set('itemPreset', str(identity) if has_equipment(environment) else '0')
        resolved.append(child)
    return item


def build(xml, output, locale, env, language):
    if output.exists() and any(output.iterdir()):
        raise SystemExit(f'Output must be empty: {output}')
    features = enabled_features(xml, locale, env)
    names = {int(k.get('id')): (k.get('name') or '').strip()
             for k in ET.parse(xml / f'string/{language}/itemname.xml').getroot() if k.get('id', '').isdigit()}
    data, models = defaultdict(list), defaultdict(list)
    counts = {'items': 0, 'resolved': 0, 'inactive': 0, 'equipment': 0}
    for path in sorted((xml / 'item').rglob('*.xml'), key=lambda p: int(p.stem)):
        identity = int(path.stem)
        environment = resolve_environment(ET.parse(path).getroot().findall('environment'), features, locale)
        prefix = path.stem[:3]
        counts['items'] += 1
        data[prefix].append(item_data(identity, environment))
        if environment is None:
            counts['inactive'] += 1
            continue
        counts['resolved'] += 1
        if not has_equipment(environment):
            continue
        counts['equipment'] += 1
        models[prefix].append(item_model(identity, environment, names.get(identity, '')))
    for folder, groups, root_tag in [('itemdata', data, 'ms2'), ('itemmodel', models, 'ms2')]:
        for prefix, elements in groups.items():
            root = ET.Element(root_tag)
            root.extend(elements)
            ET.indent(root, '\t')
            target = output / folder / f'{prefix}.xml'
            target.parent.mkdir(parents=True, exist_ok=True)
            ET.ElementTree(root).write(target, encoding='utf-8', xml_declaration=True)
    return counts


def copy_tree(xml, output, report):
    """Copy every other XML the simulator reads, taken from its KMS2-era extraction report."""
    missing = []
    for entry in json.loads(report.read_text(encoding='utf-8')):
        relative = entry['path']
        if relative.startswith(('itemmodel/', 'itemdata/')):
            continue
        source = xml / relative
        if not source.exists():
            missing.append(relative)
            continue
        target = output / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
    return missing


def provenance(xml, output, locale, env, language, counts, missing):
    files = [{'path': p.relative_to(output).as_posix(), 'sha256': hashlib.sha256(p.read_bytes()).hexdigest().upper()}
             for p in sorted(output.rglob('*.xml'))]
    return {'source': str(xml), 'locale': locale, 'environment': env, 'language': language,
            'counts': counts, 'missingFromSource': missing, 'files': files}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--xml', type=Path, required=True, help='LithMS2-XML/Xml')
    parser.add_argument('--output', type=Path, required=True, help='Empty directory to write the source layout into')
    parser.add_argument('--copy-from-report', type=Path,
                        help='extraction-report.json listing the other XML files to copy, e.g. SimulatorSources/Xml')
    parser.add_argument('--locale', default='NA')
    parser.add_argument('--env', default='Live')
    parser.add_argument('--language', default='en')
    args = parser.parse_args()
    counts = build(args.xml, args.output, args.locale, args.env, args.language)
    missing = copy_tree(args.xml, args.output, args.copy_from_report) if args.copy_from_report else []
    for extra in ['table/feature.xml', 'table/feature_setting.xml', f'string/{args.language}/itemname.xml']:
        target = args.output / extra
        if not target.exists():
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(args.xml / extra, target)
    record = provenance(args.xml, args.output, args.locale, args.env, args.language, counts, missing)
    (args.output / 'lith-sources.json').write_text(json.dumps(record, indent=1), encoding='utf-8')
    print(json.dumps({'counts': counts, 'missingFromSource': missing}, indent=2))
