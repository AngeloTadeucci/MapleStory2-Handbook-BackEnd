"""Prepare remaining inventoried models, including published models absent from the database."""
import argparse
from pathlib import Path
from kfm_source import read_kfm, referenced_file
from migration_inventory import read, write, sha


def choose(paths):
    if not paths:
        raise FileNotFoundError('No extracted original source')
    if len({sha(path) for path in paths}) != 1:
        raise ValueError('Distinct original source variants require an explicit choice')
    def priority(path):
        text = path.as_posix()
        return (0 if '/NpcSources/' in text else 1 if '/WardrobeSources/' in text else 2, text)
    return sorted(paths, key=priority)[0]


def prepare(resources, mapping, manifest, coverage, output):
    if output.exists():
        raise ValueError('Choose a fresh inventory work directory')
    resources = resources.resolve()
    known = {asset['model'] for asset in read(manifest)['assets'] if asset.get('standalone')}
    prior = read(coverage)
    known.update(prior['history'])
    known.update(row['model'] for row in prior['unresolved'])
    jobs, failures = [], []
    for row in read(mapping):
        name = row['model']
        if name in known or name.startswith('simulator-release-'):
            continue
        try:
            sources = [(resources / source['path']).resolve() for source in row['sources']]
            if any(not path.is_relative_to(resources) for path in sources):
                raise ValueError('Original source escapes resource root')
            controllers = [path for path in sources if path.suffix.lower() == '.kfm']
            clips = []
            if controllers:
                controller = choose(controllers)
                document = read_kfm(controller.read_bytes())
                model = referenced_file(controller, document['model'], resources)
                clips = document['clips']
                files = [controller, model] + [referenced_file(controller, clip['file'], resources) for clip in clips]
                plan = {'id': name, 'input': model.relative_to(resources).as_posix(),
                        'kfm': controller.relative_to(resources).as_posix(), 'clips': ['all']}
            else:
                model = choose([path for path in sources if path.suffix.lower() == '.nif'])
                files = [model]
                plan = {'id': name, 'input': model.relative_to(resources).as_posix()}
                siblings = [path for path in model.parent.iterdir() if path.suffix.lower() == '.kf']
                exact = [path for path in siblings if path.stem.lower() == model.stem.lower()]
                if exact:
                    animation = choose(exact)
                    files.append(animation)
                    plan.update(animations=model.parent.relative_to(resources).as_posix(), clips=[animation.stem])
                    clips = [{'file': animation.name, 'selection': 'all authored sequences in exact sibling KF'}]
                elif any(path.stem.lower().startswith(model.stem.lower() + '_') for path in siblings):
                    raise ValueError('External animation needs an explicit source mapping')
            plan['output'] = f'{name}/{name}.gltf'
            jobs.append({'model': name, 'sources': {path.relative_to(resources).as_posix(): sha(path) for path in files},
                         'sourceClips': clips, 'plan': plan, 'databaseReferences': row['databaseReferences'],
                         'publishedFiles': row['publishedFiles']})
        except (ValueError, OSError) as error:
            failures.append({'model': name, 'status': 'source-missing' if isinstance(error, OSError) else 'source-review-required',
                             'error': str(error), 'databaseReferences': row['databaseReferences']})
    write(output / 'jobs.json', jobs)
    write(output / 'source-failures.json', failures)
    print(f'Prepared {len(jobs)} additional inventory models; {len(failures)} source limitations', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['resources', 'mapping', 'manifest', 'coverage', 'output']:
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    prepare(args.resources, args.mapping, args.manifest, args.coverage, args.output)
