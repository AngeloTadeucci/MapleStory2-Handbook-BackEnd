"""Record coverage separately from inspected captures and inherited acceptance."""
import argparse
import hashlib
import json
from collections import Counter
from pathlib import Path


def read(path):
    return json.loads(path.read_text())


def evidence(path):
    return {'path': str(path), 'bytes': path.stat().st_size,
            'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}


def report(library, work, build, output):
    catalog = read(library / 'simulator-catalog.json')['items']
    by_pair = {(i['itemId'], i['bodyVariant']): i for i in catalog}
    inventory = read(work / 'wardrobe-inventory.json')
    validator = read(work / 'gltf-validation-14.json')
    assets = read(work / 'asset-tests-14/summary.json')
    if validator['invalid'] or validator['warningFiles'] or assets['failedBatches']:
        raise ValueError('Final asset checks did not pass')
    packaging = read(build / 'build-result.json')
    if read(work / 'packaged-files-14.json')['failed']:
        raise ValueError('Packaged release hashes differ')
    if any(row['status'] != 404 for row in read(work / 'private-public-isolation-14.json')):
        raise ValueError('Private preview assets exposed')
    if packaging['libraries'] != [library.name]:
        raise ValueError('Unexpected packaged library')
    observed, cases, files = set(), [], []
    for name in ['family-review-14', 'corrected-texture-review-14',
                 'crusader-palette-review-14', 'hair-length-review-14-unhatted']:
        folder = work / name
        captured = read(folder / 'results.json')
        if captured['errors']:
            raise ValueError('Browser capture errors: ' + name)
        files.append(evidence(folder / 'results.json'))
        for case in captured['results']:
            pairs = [(i['id'], case['body']) for i in case['after']['equipped']]
            observed.update(pairs)
            cases.append({'group': name, 'name': case['name'], 'body': case['body'],
                          'equippedItemIds': [i[0] for i in pairs],
                          'pose': case.get('pose', 'fitting_idle_a'), 'time': case.get('time', .65),
                          'expression': case.get('expression', 'default'),
                          'hairLength': case.get('hairLength'), 'palette': case.get('palette'),
                          'captures': [evidence(folder / f) for f in case['captures']]})
        files.extend(evidence(p) for p in sorted(folder.glob('contact-*.png')))
    public = read(work / 'ui-built-14-adjustable/results.json')
    detailed = read(work / 'ui-workflows-14-complete/outfit-workflows.json')
    if public['errors'] or detailed['errors']:
        raise ValueError('Final UI errors')
    if len([s for s in public['states'] if 'body' in s]) != 2:
        raise ValueError('Both compiled UI bodies were not completed')
    if any(s.get('hairLengthControlsTested', 0) < 1 for s in public['states'] if 'body' in s):
        raise ValueError('Compiled hair reset workflow did not exercise adjustable controls')
    if not any(s.get('privateRegression') for s in detailed['states']):
        raise ValueError('Private regression did not complete')
    files.extend(evidence(p) for p in [work / 'ui-built-14-adjustable/results.json',
        work / 'ui-workflows-14-complete/outfit-workflows.json', work / 'preserved-baselines.json',
        work / 'texture-provenance-audit.json', work / 'source-label-audit.json',
        work / 'gltf-validation-14.json', work / 'asset-tests-14/summary.json',
        work / 'packaged-files-14.json', work / 'private-public-isolation-14.json',
        work / 'frontend-source-14c.json',
        work / 'frontend-tests-14-reset.log', work / 'frontend-check-14-reset.log',
        work / 'csharp-tests-09.log', work / 'python-tests-final.log',
        work / 'reference-crusader-equipped.json', work / 'reference-crusader-equipped.png',
        build / 'build-result.json'])
    unavailable = Counter(reason for i in catalog if i['availability'] == 'unavailable'
                          for reason in i['blockers'])
    result = {
        'version': 1, 'release': library.name, 'date': '2026-09-07',
        'scope': {'sourceItemIds': inventory['sourceItemCount'],
                  'scopedItemIds': len({i['itemId'] for i in catalog}), 'bodyPairs': len(catalog),
                  'bodyCounts': dict(Counter(i['bodyVariant'] for i in catalog)),
                  'searchableEntries': len(catalog), 'sourceFamilies': len({i['family'] for i in catalog}),
                  'excluded': dict(Counter(i['reason'] for i in inventory['excluded']))},
        'conversion': {**read(library / 'coverage.json'),
            'completeBundleOrCustomizationFamilies': len({i['family'] for i in catalog if i['conversion'] in ['converted', 'baseline-reused', 'customization-only']}),
            'originalModelJobsAttempted': len(read(work / 'expansion/jobs.json'))},
        'appearance': {'inheritedVerifiedPairs': sum(i['availability'] == 'verified' for i in catalog),
                      'newlyPromotedPairs': 0, 'visuallyInspectedBodyPairs': len(observed),
                      'capturedOutfitCases': len(cases),
                      'reviewScope': 'These captures were inspected as previews. Conversion, successful interaction and inspection do not establish complete client appearance parity.',
                      'entries': [{'itemId': pair[0], 'bodyVariant': pair[1],
                                   'availability': by_pair[pair]['availability'],
                                   'family': by_pair[pair]['family']} for pair in sorted(observed)],
                      'cases': cases},
        'verification': {'csharpTests': 45, 'pythonTests': 17, 'frontendLogicAndSourceTests': 115,
                         'assetTests': assets, 'gltf': validator,
                         'compiledUiBodies': ['female', 'male'], 'privateGeloRegression': True,
                         't3': 'Unavailable: no preview automation host for this environment',
                         'baselinePreservation': read(work / 'preserved-baselines.json')},
        'limitations': [
            'No complete visual coverage claim. All new entries remain preview or unavailable.',
            '11400049 on both bodies matches the reference cross/coat coloring with explicit client palette10/index9. Missing defaultColorIndex fallback remains unresolved and visible in the catalog.',
            '10200006/female cannot fit hat11300001 because its required hair form is unavailable. The interaction reports the reason instead of substituting hair.',
            'The capture named matching-revision-jellyfish in family-review-14 is item11301482/female, whose selected source preset is11301385 resort wear. Actual corrected jellyfish assets were separately captured using11320265/male and11320266/female.',
            'Paired star stow overlap and unsupported drawn anchors remain visible limitations.',
            'Private ponytail controls, some animation/material/light controllers and multi-axis dummy rotations remain blocked with source evidence.',
            'Missing sources, nonvisual equipment and UGC templates lacking user-supplied artwork remain discoverable; no private art was supplied or harvested.'
        ],
        'unavailableReasonCountsNonexclusive': unavailable.most_common(),
        'evidence': files,
        'releaseInventory': evidence(library / 'release-inventory.json'),
        'buildDirectory': str(build), 'databaseWrites': 0, 'published': False,
        'committed': False, 'pushed': False
    }
    output.write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps({'release': result['release'], 'scope': result['scope'],
                      'visuallyInspectedBodyPairs': len(observed), 'capturedOutfitCases': len(cases)}, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--library', type=Path, required=True)
    parser.add_argument('--work', type=Path, required=True)
    parser.add_argument('--build', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    report(args.library, args.work, args.build, args.output)
