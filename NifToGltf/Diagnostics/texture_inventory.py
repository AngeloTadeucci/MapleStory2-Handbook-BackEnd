"""Inventory all external NIF textures, including references hidden by a first conversion error."""
import argparse
from collections import defaultdict
import json
from pathlib import Path
from inspect_nif import read_document, object_net
from scan_nif import Reader


def inventory(root, texture_roots):
    available = defaultdict(list)
    for directory in texture_roots:
        for path in directory.rglob('*'):
            if path.suffix.lower() == '.dds':
                available[path.name.lower()].append(path)
    missing = defaultdict(set)
    unreadable = []
    for path in sorted(root.rglob('*.nif')):
        try:
            strings, blocks, _ = read_document(path)
            local = {p.name.lower() for p in path.parent.iterdir() if p.is_file()}
            for kind, data in blocks:
                if kind != 'NiSourceTexture':
                    continue
                reader = Reader(data)
                object_net(reader, strings)
                if not reader.number('B'):
                    continue
                name = strings[reader.number()].replace('\\', '/')
                filename = Path(name).name.lower()
                if filename not in local and filename not in available:
                    missing[name].add(path.relative_to(root).as_posix())
        except (ValueError, IndexError, OSError) as error:
            unreadable.append({'input': path.relative_to(root).as_posix(), 'error': str(error)})
    return {'missing': [{'texture': name, 'models': sorted(models)} for name, models in sorted(missing.items())],
            'unreadable': unreadable}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('input', type=Path)
    parser.add_argument('--textures', nargs='+', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = inventory(args.input, args.textures)
    args.output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(f"{len(result['missing'])} missing texture names, {len(result['unreadable'])} unreadable NIFs")
