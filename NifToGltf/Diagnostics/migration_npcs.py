"""Build and execute resumable NPC KFM jobs from extracted original sources.

Each source controller keeps its own model identity and all authored clips.
Failures remain in reports and cause a nonzero exit, without partial promotion.
"""
import argparse
from collections import defaultdict
import hashlib
import json
import math
from pathlib import Path
import shutil
import subprocess
from kfm_source import read_kfm, referenced_file
from migration_inventory import read, write, sha


def prepare(resources, source, database, output):
    resources, source = resources.resolve(), source.resolve()
    names = {r['kfm'].lower() for r in read(database)['npcs'] if r['kfm']}
    jobs, failures = [], []
    by_name = defaultdict(list)
    for kfm in source.rglob('*.kfm'):
        by_name[kfm.stem.lower()].append(kfm)
    for name in sorted(names):
        matches = by_name.get(name, [])
        if len(matches) != 1:
            failures.append({'model': name, 'status': 'source-missing' if not matches else 'ambiguous-source', 'matches': [str(p) for p in matches]})
            continue
        kfm = matches[0]
        try:
            record = read_kfm(kfm.read_bytes())
            model = referenced_file(kfm, record['model'], resources)
            files = [kfm, model] + [referenced_file(kfm, c['file'], resources) for c in record['clips']]
            provenance = {p.relative_to(resources).as_posix(): sha(p) for p in files}
            jobs.append({'model': name, 'sources': provenance, 'sourceClips': record['clips'],
                         'plan': {'id': name, 'input': model.relative_to(resources).as_posix(),
                                  'kfm': kfm.relative_to(resources).as_posix(), 'clips': ['all'],
                                  'output': f'{name}/{name}.gltf'}})
        except (ValueError, OSError) as error:
            status = 'source-missing' if isinstance(error, OSError) or str(error).startswith('Missing or ambiguous') else 'unsupported-source-format'
            failures.append({'model': name, 'status': status, 'error': str(error)})
    write(output / 'jobs.json', jobs)
    write(output / 'source-failures.json', failures)
    print(json.dumps({'jobs': len(jobs), 'sourceFailures': len(failures)}))


def run_groups(resources, work, converter, textures, size, reserve_gib=8):
    jobs = read(work / 'jobs.json')
    texture_files = {str(p): sha(p) for root in textures.split(';') for p in sorted(Path(root).rglob('*.dds'))}
    version = {'binary': sha(converter), 'textures': texture_files}
    version_hash = hashlib.sha256(json.dumps(version, sort_keys=True).encode()).hexdigest()
    write(work / 'converter-texture-provenance.json', version)
    results = []
    for start in range(0, len(jobs), size):
        group = jobs[start:start + size]
        signature = hashlib.sha256(json.dumps({'version': version_hash, 'jobs': group}, sort_keys=True).encode()).hexdigest()
        folder = work / 'groups' / signature[:24]
        checkpoint = folder / 'checkpoint.json'
        if checkpoint.exists():
            previous = read(checkpoint)
            for record in previous:
                if record.get('sha256') and sha(Path(record['file'])) != record['sha256']:
                    raise ValueError('Completed NPC output changed: ' + record['model'])
            results.extend(previous)
            continue
        if shutil.disk_usage(work).free < reserve_gib * 1024**3 + 256 * 1024**2:
            write(work / 'results.json', results)
            raise RuntimeError(f'NPC conversion reached the {reserve_gib:g} GiB disk reserve')
        for job in group:
            for name, expected in job['sources'].items():
                if sha(resources / name) != expected:
                    raise ValueError('Prepared source changed: ' + name)
        folder.mkdir(parents=True, exist_ok=True)
        plan = folder / 'plan.json'
        write(plan, {'version': 1, 'models': [job['plan'] for job in group]})
        target = folder / 'assets'
        if target.exists():
            raise ValueError('Uncheckpointed group output needs inspection: ' + str(target))
        with (folder / 'converter.log').open('w') as log:
            result = subprocess.run(['dotnet', str(converter), '--native', '--batch', '--input', str(resources),
                '--output', str(target), '--manifest', str(plan), '--textures', textures], stdout=log, stderr=subprocess.STDOUT)
        report = read(target / 'batch-report.json') if (target / 'batch-report.json').exists() else {}
        manifest = read(target / 'native-manifest.json') if (target / 'native-manifest.json').exists() else {'assets': []}
        successful = {a['id']: a for a in manifest['assets']}
        completed = []
        for job in group:
            asset = successful.get(job['model'])
            record = {'model': job['model'], 'signature': signature, 'directory': str(target),
                      'status': 'converted-unreviewed' if asset else 'unsupported-feature', 'asset': asset}
            if asset:
                path = target / asset['uri']
                record.update(file=str(path), sha256=sha(path))
            else:
                record['errors'] = [e for kind in ['failed', 'missing', 'excluded'] for e in report.get(kind, []) if e['input'] == job['plan']['input']]
                if not record['errors']:
                    record['errors'] = [{'error': 'Converter produced no result', 'exitCode': result.returncode}]
            completed.append(record)
        write(checkpoint, completed)
        results.extend(completed)
        write(work / 'results.json', results)
        print(json.dumps({'completed': len(results), 'total': len(jobs), 'converted': sum(r['status'] == 'converted-unreviewed' for r in results)}), flush=True)
    return int(any(r['status'] != 'converted-unreviewed' for r in results) or bool(read(work / 'source-failures.json')))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['prepare', 'run'])
    parser.add_argument('--resources', type=Path, required=True)
    parser.add_argument('--work', type=Path, required=True)
    parser.add_argument('--source', type=Path)
    parser.add_argument('--database', type=Path)
    parser.add_argument('--converter', type=Path)
    parser.add_argument('--textures')
    parser.add_argument('--batch-size', type=int, default=32)
    parser.add_argument('--reserve-gib', type=float, default=8, help='Free space retained between batches')
    args = parser.parse_args()
    if args.command == 'prepare':
        prepare(args.resources, args.source, args.database, args.work)
    else:
        if args.batch_size < 1:
            parser.error('--batch-size must be positive')
        if not math.isfinite(args.reserve_gib) or args.reserve_gib < 1 or args.batch_size < 1:
            parser.error('Reserve must be at least 1 GiB and batch size must be positive')
        raise SystemExit(run_groups(args.resources, args.work, args.converter, args.textures, args.batch_size, args.reserve_gib))
