"""Extract authored hair placements without modifying any release assets."""
import argparse
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET


def extract(path):
    raw = Path(path).read_bytes()
    entries = {}
    for item in ET.fromstring(raw).findall('ItemModel'):
        parts = []
        for asset in item.findall('./slots/slot/asset'):
            presets = asset.findall('custom')
            if not presets:
                continue
            def vector(preset, name):
                values = [float(value) for value in preset.attrib[name].split(',')]
                if len(values) != 3:
                    raise ValueError('Expected three source components')
                return values
            parts.append({
                'source': asset.attrib['name'].replace('\\', '/').split('/')[-1].removeprefix('urn:').removesuffix('.nif').lower(),
                'selfNode': asset.attrib['selfnode'],
                'targetNode': asset.attrib['targetnode'],
                'presets': [{'position': vector(p, 'position'), 'rotation': vector(p, 'rotation')} for p in presets],
                'jointAngles': [dict(joint.attrib) for joint in asset.findall('jointangle')]
            })
        if parts:
            entries[item.attrib['id']] = parts
    return {'sourceSha256': hashlib.sha256(raw).hexdigest(), 'items': entries}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('xml')
    args = parser.parse_args()
    print(json.dumps(extract(args.xml), indent=2))
