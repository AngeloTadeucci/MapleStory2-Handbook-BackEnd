"""Prepare whole-source previews where equipment-part exports have no canonical choice.

Distinct original NIFs and face-preset ambiguities stay explicit. KFM or exact
sibling KF references preserve authored animation; unresolved clip mappings fail.
Execution uses migration_npcs.py's resumable source-hashed batch format.
"""
import argparse
import json
from pathlib import Path
from kfm_source import read_kfm, referenced_file
from migration_inventory import read, write, sha, safe_path


def prepare(resources, manifest, variants, output):
    if output.exists():
        raise ValueError('Choose a fresh standalone work directory')
    resources = resources.resolve()
    assets = {asset['id']: asset for asset in read(manifest)['assets']}
    jobs, failures = [], []
    for model in read(variants)['models']:
        name = model['model']
        if model['defaultAssetId'] is not None or name in {'f_body', 'm_body'}:
            continue
        members = [assets[identity] for identity in model['variants']]
        inputs = sorted({str(safe_path(asset['input'])) for asset in members})
        record = {'model': name, 'inputs': inputs, 'variants': model['variants']}
        try:
            if any(not (resources / path).resolve().is_relative_to(resources) for path in inputs):
                raise ValueError('Original model escapes the source root')
            if any(asset.get('slot') == 'FA' for asset in members):
                raise ValueError('Face presets require an explicit standalone identity')
            hashes = {path: sha(resources / path) for path in inputs}
            if len(set(hashes.values())) != 1:
                raise ValueError('Distinct original NIF variants share this model name')
            source = resources / inputs[0]
            plan = {'id': name, 'input': inputs[0], 'output': f'{name}/{name}.gltf'}
            siblings = list(source.parent.iterdir())
            kfms = [path for path in siblings if path.name.lower() == source.with_suffix('.kfm').name.lower()]
            kfs = [path for path in siblings if path.name.lower() == source.with_suffix('.kf').name.lower()]
            clips = []
            files = []
            if len(kfms) > 1 or len(kfs) > 1:
                raise ValueError('Ambiguous case-insensitive animation reference')
            if kfms:
                kfm = kfms[0]
                parsed = read_kfm(kfm.read_bytes())
                declared = referenced_file(kfm, parsed['model'], resources)
                if sha(declared) != hashes[inputs[0]]:
                    raise ValueError('KFM declares a different original model')
                files = [kfm, declared] + [referenced_file(kfm, clip['file'], resources) for clip in parsed['clips']]
                plan.update(input=declared.relative_to(resources).as_posix(), kfm=kfm.relative_to(resources).as_posix(), clips=['all'])
                clips = parsed['clips']
            elif kfs:
                files = kfs
                plan.update(animations=source.parent.relative_to(resources).as_posix(), clips=[kfs[0].stem])
                clips = [{'file': kfs[0].name, 'selection': 'all authored sequences in exact sibling KF'}]
            elif any(asset['clips'] for asset in members) or any(
                    path.suffix.lower() == '.kf' and path.stem.lower().startswith(source.stem.lower() + '_') for path in siblings):
                raise ValueError('External animation needs an explicit source mapping')
            hashes.update({path.relative_to(resources).as_posix(): sha(path) for path in files})
            jobs.append({'model': name, 'sources': hashes, 'sourceClips': clips, 'plan': plan,
                         'equipmentVariants': model['variants']})
        except (ValueError, OSError) as error:
            failures.append({**record, 'status': 'source-review-required', 'error': str(error)})
    write(output / 'jobs.json', jobs)
    write(output / 'source-failures.json', failures)
    print(json.dumps({'jobs': len(jobs), 'sourceReviewRequired': len(failures)}), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for argument in ['resources', 'manifest', 'variants', 'output']:
        parser.add_argument('--' + argument, type=Path, required=True)
    args = parser.parse_args()
    prepare(args.resources, args.manifest, args.variants, args.output)
