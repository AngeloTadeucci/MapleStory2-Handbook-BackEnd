"""Create an explicit static batch plan, with separately documented effect exclusions."""
import argparse
import json
from pathlib import Path


def create_plan(root, scope):
    exclusions = {entry['input'].lower(): entry['reason'] for entry in scope['effects']}
    if scope['version'] != 1 or len(exclusions) != len(scope['effects']):
        raise ValueError('Invalid or duplicate exclusions')
    models = []
    found = set()
    for path in sorted(root.rglob('*.nif')):
        name = path.relative_to(root).as_posix()
        entry = {'input': name}
        if name.lower() in exclusions:
            entry['excludeEffect'] = exclusions[name.lower()]
            found.add(name.lower())
        models.append(entry)
    if found != exclusions.keys():
        raise ValueError(f'Excluded source files missing: {exclusions.keys() - found}')
    return {'version': 1, 'models': models}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('input', type=Path)
    parser.add_argument('--scope', type=Path, default=Path(__file__).with_name('scope-exclusions.json'))
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = create_plan(args.input, json.loads(args.scope.read_text(encoding='utf-8')))
    args.output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(f"{len(result['models'])} inputs, {sum('excludeEffect' in model for model in result['models'])} explicit exclusions")
