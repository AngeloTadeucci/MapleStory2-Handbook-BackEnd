"""Audit exact source names and icon paths without requiring a database."""
import argparse
import re
from pathlib import Path
from wardrobe_inventory import write
from expand_wardrobe import read, sha


def audit(inventory, image_index, output):
    index = {p.lower() for p in read(image_index)}
    entries = []
    for item in read(inventory)['items']:
        path = (item.get('sourceIcon') or '').replace('\\', '/').lower().removeprefix('./').removeprefix('data/resource/image/')
        entries.append({'itemId': item['itemId'], 'bodyVariant': item['bodyVariant'],
                        'namePresent': bool(item['sourceName']), 'usableName': bool(item['sourceName']) and not bool(re.match(r'ITEMNAME_\d+_', item['sourceName'], re.I)), 'icon': path, 'iconInArchive': path in index})
    counts = {'bodyPairs': len(entries), 'unnamedPairs': sum(not i['namePresent'] for i in entries),
              'unusableNamePairs': sum(not i['usableName'] for i in entries),
              'missingIconPairs': sum(not i['iconInArchive'] for i in entries)}
    write(output, {'inventoryHash': sha(inventory), 'imageIndexHash': sha(image_index), 'counts': counts, 'entries': entries})
    print(counts)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--inventory', type=Path, required=True)
    parser.add_argument('--image-index', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    audit(args.inventory, args.image_index, args.output)
