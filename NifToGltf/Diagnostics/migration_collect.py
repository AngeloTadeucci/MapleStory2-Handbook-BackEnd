"""Collect successful local checkpoints without hiding unresolved model failures."""
import argparse
import os
from pathlib import Path
from migration_inventory import read, write, sha, safe_path


def collect(workspaces, output):
    if output.exists():
        raise ValueError('Choose a fresh collection directory')
    records, histories, limitations = {}, {}, {}
    for work in workspaces:
        completed = read(work / 'results.json')
        jobs = work / 'jobs.json'
        if jobs.exists() and {row['model'] for row in read(jobs)} != {row['model'] for row in completed}:
            raise ValueError('Incomplete conversion workspace: ' + str(work))
        invalidations = work / 'invalidates.json'
        if invalidations.exists():
            for name, reason in read(invalidations).items():
                records.pop(name, None)
                histories.setdefault(name, []).append({'work': str(work), 'status': 'invalidates-prior-export', 'reason': reason})
        for limitation in read(work / 'source-failures.json'):
            limitations[limitation['model']] = limitation
        for row in completed:
            name = row['model']
            histories.setdefault(name, []).append({'work': str(work), 'status': row['status'],
                                                   'errors': row.get('errors', []), 'sha256': row.get('sha256')})
            if row.get('asset') or not records.get(name, {}).get('asset'):
                records[name] = row
    # Validate all completed bytes before making a collection.
    for row in records.values():
        if row.get('asset') and sha(Path(row['file'])) != row['sha256']:
            raise ValueError('Completed output changed: ' + row['model'])
    assets = []
    for name, row in sorted(records.items()):
        if not row.get('asset'):
            continue
        asset = dict(row['asset'])
        target = output / safe_path(asset['uri'])
        target.parent.mkdir(parents=True, exist_ok=True)
        os.link(row['file'], target)
        assets.append(asset)
    write(output / 'native-manifest.json', {'version': 1, 'coordinateSystem': 'gltf-y-up-meters', 'assets': assets})
    unresolved = [row for row in records.values() if not row.get('asset')]
    unresolved += [row for name, row in limitations.items() if name not in records]
    write(output / 'conversion-coverage.json', {'convertedUnreviewed': len(assets), 'unresolved': unresolved,
          'history': histories, 'acceptance': 'Local candidate only; schema validation does not establish client parity'})
    print(f'Collected {len(assets)} models; {len(unresolved)} unresolved')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work', type=Path, nargs='+', required=True, help='Oldest to newest checkpoint directories')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    collect(args.work, args.output)
