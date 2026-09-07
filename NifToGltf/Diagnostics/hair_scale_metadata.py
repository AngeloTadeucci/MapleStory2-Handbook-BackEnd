"""Extract client hair UI scale records without changing release assets."""
import argparse
import hashlib
import json
import math
from pathlib import Path
import xml.etree.ElementTree as ET


def extract(path):
    raw = Path(path).read_bytes()
    items = {}
    for item in ET.fromstring(raw).findall('ItemModel'):
        customize = item.find('customize')
        scales = []
        for scale in item.findall('./slots/slot/scale'):
            # KMS2 constructor 0x140544b10; XML parser 0x140544b70.
            minimum = float(scale.get('min', '0'))
            maximum = float(scale.get('max', '1'))
            values = [float(v) for v in scale.get('value', '').split(',') if v.strip()]
            if not all(math.isfinite(v) and v >= 0 for v in [minimum, maximum, *values]):
                raise ValueError(f'Invalid scale for {item.get("id")}')
            if minimum > maximum:
                raise ValueError(f'Reversed bounds for {item.get("id")}')
            reverse = int(scale.get('reverse', '0'))
            if reverse not in (0, 1):
                raise ValueError(f'Unknown reverse flag for {item.get("id")}')
            scales.append(dict(min=minimum, max=maximum, reverse=bool(reverse), values=values))
        item_id = item.attrib['id']
        if item_id in items:
            raise ValueError(f'Duplicate item {item_id}')
        items[item_id] = dict(enabled=customize is not None and customize.get('scale') == '1',
                              scales=scales)
    return dict(sourceSha256=hashlib.sha256(raw).hexdigest(), items=items)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('xml', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = extract(args.xml)
    args.output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(f'Extracted {len(result["items"])} hair scale definitions')
