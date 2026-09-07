"""Read hairstyle capTransform defaults without modifying release assets."""
import argparse
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET


def extract(path):
    raw = Path(path).read_bytes()
    entries = {}
    for item in ET.fromstring(raw).findall('ItemModel'):
        transform = item.find('./customize/capTransform')
        if transform is None:
            continue
        values = {key: [float(v) for v in transform.attrib[key].split(',')]
                  for key in ('position', 'rotation')}
        if any(len(vector) != 3 for vector in values.values()):
            raise ValueError('Expected three source components')
        entries[item.attrib['id']] = values
    return {'sourceSha256': hashlib.sha256(raw).hexdigest(), 'items': entries}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('xml')
    print(json.dumps(extract(parser.parse_args().xml), indent=2))
