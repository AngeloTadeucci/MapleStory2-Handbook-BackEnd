"""Restartable source-family batches and immutable-base candidate assembly.

Use prepare, then run with an explicit batch bound, then assemble. No publishing.
Every checkpoint binds source, skeleton, texture inventory and converter hashes.
Disk guards leave 8 GiB plus a per-batch reserve. Failed entries stay discoverable.
"""
import argparse
from concurrent.futures import ThreadPoolExecutor
import fcntl
from collections import Counter, defaultdict
import copy
import hashlib
import json
from pathlib import Path
import platform
import shutil
import subprocess
from wardrobe_inventory import digest, write
from inspect_nif import read_document
from kfm_source import animation_inputs


def read(path):
    return json.loads(path.read_text(encoding='utf-8'))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def check_disk(path, reserve=0):
    free = shutil.disk_usage(path).free
    if free < (8 * 1024**3 + reserve):
        raise RuntimeError(f'Disk checkpoint: {free / 1024**3:.2f} GiB free, requires 8 GiB plus batch reserve')
    return free


def prepare(args):
    if (args.work / 'jobs.json').exists():
        raise ValueError('Prepared jobs exist; use run to resume or choose a fresh work directory')
    inv = read(args.inventory)
    resource = args.resources
    source_hashes = {}
    converter = {p.as_posix(): sha(p) for p in sorted(Path('NifToGltf/Native').glob('*.cs'))}
    texture_records = []
    for root in [resource / 'Models/Textures', resource / 'WardrobeSources']:
        for p in sorted(root.rglob('*.dds')):
            texture_records.append({'path': str(p), 'sha256': sha(p)})
    texture_hash = digest(texture_records)
    write(args.work / 'texture-provenance.json', texture_records)
    jobs, entries = {}, []
    for item in inv['items']:
        entry = {k: copy.deepcopy(item[k]) for k in ['itemId', 'bodyVariant', 'sourceName', 'sourceIcon', 'isOutfit', 'slots', 'customize', 'cutting', 'classification', 'family', 'presetId']}
        entry.update(parts=[], blockers=list(item['blockers']), limitations=[])
        if item['sourceProperties'].get('ucc', {}).get('mesh'):
            entry['blockers'].append('UGC template requires supplied artwork; no private artwork is included')
        effects = [child for env in item['environments'] for child in env['children'] if 'effect' in child['tag'].lower()]
        if effects:
            entry['limitations'].append('Additional client effect requirements are recorded in the source inventory; only the existing hair-twinkle family is implemented')
        body = item['bodyVariant']
        skeleton = f'Models/Character/{body}/{body[0]}_body.nif'
        for number, part in enumerate(item['parts']):
            if not part['source'] or item['classification'] == 'nonvisual':
                continue
            source = resource / 'WardrobeSources' / part['source']
            if not source.exists():
                entry['blockers'].append(f'Indexed source is not extracted: {part["source"]}')
                continue
            key = str(source)
            source_hashes.setdefault(key, sha(source))
            model = {'input': 'WardrobeSources/' + part['source'], 'bodyVariant': body, 'slot': part['slot'],
                     'skeleton': skeleton, 'itemModel': 'WardrobeSources/Xml/' + item['itemModel'], 'itemId': str(item['presetId'])}
            try:
                kfm, kfs = animation_inputs(source, resource / 'WardrobeSources', part.get('kfm'))
            except (ValueError, OSError) as error:
                entry['blockers'].append(f'Animation source resolution failed for {part["source"]}: {error}')
                continue
            for animation in ([kfm] if kfm is not None else []) + kfs:
                source_hashes.setdefault(str(animation), sha(animation))
            if kfm is not None:
                model.update(kfm=kfm.relative_to(resource).as_posix(), clips=['all'])
            # A KF without a KFM still carries required ordinary animation.
            if kfs and kfm is None:
                model.update(animations=source.parent.relative_to(resource).as_posix(), clips=[p.stem for p in kfs])
            variants = [(None, model)]
            if part['slot'] == 'OH':
                variants = [('stowed', model)] + [(hand, {**model, 'hand': hand}) for hand in ['RH', 'LH']]
            elif part['slot'] in ['RH', 'LH'] and part['attributes'].get('attachnode'):
                variants = [(None, {**model, 'drawn': True})]
            for placement, plan in variants:
                signature = {'source': source_hashes[key], 'skeleton': sha(resource / skeleton),
                             'part': part, 'cutting': item['cutting'], 'body': body,
                             'placement': placement, 'drawn': plan.get('drawn'), 'textures': texture_hash,
                             'converter': digest(converter), 'animation': {str(p): sha(p) for p in ([kfm] if kfm is not None else []) + kfs}}
                identity = 'wardrobe-' + digest(signature)[:24]
                plan.update(id=identity, output=f'wardrobe/{identity}.gltf')
                jobs.setdefault(identity, {'plan': plan, 'signature': signature, 'priority': priority(item['slots']), 'items': []})['items'].append([item['itemId'], body])
                if placement in ['RH', 'LH']:
                    entry.setdefault('handParts', {}).setdefault(placement, []).append(identity)
                elif placement == 'stowed':
                    entry.setdefault('stowedParts', []).append(identity)
                else:
                    entry['parts'].append({'assetId': identity, 'slot': part['slot']})
            if part['slot'] == 'HR':
                for form in ['c', 'd']:
                    alternate = source.with_name(source.stem[:-1] + form + '.nif') if source.stem.endswith('_a') else None
                    if alternate and alternate.exists():
                        identity = 'wardrobe-' + digest({'source': sha(alternate), 'body': body, 'attach': 'Bip01 Head', 'textures': texture_hash, 'converter': converter})[:24]
                        plan = {'id': identity, 'input': alternate.relative_to(resource).as_posix(), 'output': f'wardrobe/{identity}.gltf', 'bodyVariant': body, 'slot': 'HR', 'skeleton': skeleton, 'itemModel': model['itemModel'], 'itemId': model['itemId'], 'alternateOf': model['input']}
                        jobs.setdefault(identity, {'plan': plan, 'priority': 2, 'items': [], 'signature': {'source': sha(alternate), 'body': body, 'textures': texture_hash}})['items'].append([item['itemId'], body])
                        entry.setdefault('hairForms', {}).setdefault(form, []).append(identity)
        if entry.get('handParts'):
            entry['parts'] = [{'assetId': p, 'slot': 'RH'} for p in entry['handParts']['RH']]
        if 'CP' in item['slots']:
            forms = {Path(p['source']).stem[-1] for p in item['parts'] if p['source']}
            if len(forms) == 1 and forms <= {'a', 'c', 'd'}:
                entry['hatHairForm'] = next(iter(forms))
            else:
                entry['blockers'].append('Hat hair-form selection requires source fitting evidence')
        if item['slots'] == ['HR']:
            scales = [c['attributes'].get('value', '') for s in item['modelRecord']['children'] if s['tag'] == 'slots' for slot in s['children'] for c in slot['children'] if c['tag'] == 'scale']
            entry['hairScales'] = [[float(v) for v in values.split(',') if v] for values in scales]
        if item['slots'] == ['FD']:
            entry['blockers'].append('Makeup texture, mask and placement conversion pending')
        if item['slots'] == ['FA']:
            entry['blockers'].append('Face texture and expression sequence conversion pending')
        entries.append(entry)
    ordered = sorted(jobs.values(), key=lambda j: (j['priority'], -len(j['items']), j['plan']['id']))
    write(args.work / 'jobs.json', ordered)
    write(args.work / 'entries.json', entries)
    write(args.work / 'provenance.json', {'inventoryHash': sha(args.inventory), 'converter': converter,
          'textureHash': texture_hash, 'platform': platform.platform(), 'machine': platform.machine(), 'sourceHashes': source_hashes})
    print(json.dumps({'jobs': len(jobs), 'entries': len(entries), 'priorities': dict(Counter(j['priority'] for j in ordered))}))


def priority(slots):
    if set(slots) <= {'CL', 'PA', 'GL', 'SH'}: return 0
    if set(slots) <= {'CP', 'HR', 'FA', 'FD', 'EY', 'FH', 'EA', 'ER', 'RI', 'BE', 'PD'}: return 1
    return 2


def run(args):
    with (args.work / 'runner.lock').open('w') as lock:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        provenance = read(args.work / 'provenance.json')
        if sha(args.inventory) != provenance['inventoryHash']:
            raise ValueError('Prepared source inventory changed')
        if not args.converter:
            for path, expected in provenance['converter'].items():
                if sha(Path(path)) != expected: raise ValueError('Converter changed; provide an explicit versioned binary')
        converter = {'binary': str(args.converter), 'binaryHash': sha(args.converter),
                     'sourceHashes': {p.as_posix(): sha(p) for p in sorted(Path('NifToGltf/Native').glob('*.cs'))}} if args.converter else {'preparedSource': provenance['converter']}
        command = ['dotnet', str(args.converter)] if args.converter else ['dotnet', 'run', '--no-build', '--project', 'NifToGltf', '--']
        for path, expected in provenance['sourceHashes'].items():
            if sha(Path(path)) != expected: raise ValueError('Source changed: ' + path)
        jobs = read(args.work / 'jobs.json')
        completed = set()
        for checkpoint in sorted(args.work.glob('batches/*/checkpoint.json')):
            completed.update(p['id'] for p in read(checkpoint.parent / 'plan.json')['models'])
        pending = [j for j in jobs if j['plan']['id'] not in completed]
        batches = [pending[offset:offset + args.batch_size] for offset in
                   range(0, min(len(pending), args.batches * args.batch_size), args.batch_size)]
        def convert(batch):
            free = check_disk(args.work, args.workers * len(batch) * 32 * 1024**2)
            batch_id = digest({'models': [j['plan'] for j in batch], 'converter': converter})[:20]
            output = args.work / 'batches' / batch_id
            if output.exists():
                raise ValueError(f'Incomplete batch retained at {output}; inspect before retrying')
            plan_path = args.work / 'plans' / (batch_id + '.json')
            write(plan_path, {'version': 1, 'models': [j['plan'] for j in batch]})
            print(f'Batch {batch_id}: {len(batch)} models, {free / 1024**3:.2f} GiB free', flush=True)
            result = subprocess.run(command + ['--native', '--batch',
                '--input', str(args.resources), '--textures', str(args.resources / 'Models/Textures') + ';' + str(args.resources / 'WardrobeSources'),
                '--manifest', str(plan_path), '--output', str(output)], stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
            (output / 'conversion.log').write_text(result.stdout)
            if not (output / 'batch-report.json').exists(): raise RuntimeError(f'Batch failed without checkpoint: {batch_id}')
            shutil.copy2(plan_path, output / 'plan.json')
            report = read(output / 'batch-report.json')
            write(output / 'checkpoint.json', {'exitCode': result.returncode, 'freeBytesBefore': free,
                  'freeBytesAfter': check_disk(args.work), 'converter': converter, 'platformProvenanceHash': sha(args.work / 'provenance.json')})
            print(json.dumps({'batch': batch_id, **{k: report[k] for k in ['convertedCount', 'failedCount', 'missingCount', 'excludedCount']}}), flush=True)
        # Each wave reserves disk for all its workers before starting any of them.
        for offset in range(0, len(batches), args.workers):
            check_disk(args.work, args.workers * args.batch_size * 32 * 1024**2)
            with ThreadPoolExecutor(max_workers=args.workers) as pool:
                list(pool.map(convert, batches[offset:offset + args.workers]))


def assemble(args):
    if args.output.exists(): raise ValueError('Choose a fresh candidate path')
    check_disk(args.output.parent, 256 * 1024**2)
    for record in read(args.base / 'release-inventory.json')['files']:
        if sha(args.base / record['path']) != record['sha256']: raise ValueError('Baseline changed: ' + record['path'])
    shutil.copytree(args.base, args.output)
    base_catalog = read(args.base / 'simulator-catalog.json')
    base_entries = {(i['itemId'], i['bodyVariant']): i for i in base_catalog['items']}
    entries = read(args.work / 'entries.json')
    sources = {(i['itemId'], i['bodyVariant']): i for i in read(args.inventory)['items']}
    known = {(i['itemId'], i['bodyVariant']): i for i in entries}
    visual_notes = read(Path('NifToGltf/Diagnostics/wardrobe-visual-notes.json'))['notes']
    inherited = {known[k]['family']: v for k, v in base_entries.items() if k in known and v['availability'] != 'unavailable'}
    customization = read(args.base / 'customization.json')
    extra = read(args.customization / 'metadata.json') if args.customization else {'faces': {}, 'makeup': {}, 'failures': {}}
    for key, value in extra['faces'].items():
        customization['faces'].setdefault(key, value)
    if args.customization:
        for source in sorted((args.customization / 'textures').rglob('*')):
            if not source.is_file() or source.name == 'texture-report.json': continue
            relative = source.relative_to(args.customization / 'textures')
            kind = 'makeup' if relative.parts[0] == 'item_makeup' else 'faces'
            target = args.output / kind / relative
            if not target.exists():
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(source, target)
    write(args.output / 'customization.json', customization)
    manifest = read(args.base / 'native-manifest.json')
    assets = {a['id']: a for a in manifest['assets']}
    errors = {}
    checkpoints = sorted(args.work.glob('batches/*/checkpoint.json'))
    if args.patches: checkpoints += sorted(args.patches.glob('*/checkpoint.json'), key=lambda p: (read(p).get('generation', 1 if read(p).get('selectionHash') else 0), p.parent.name))
    asset_sources = {identity: args.base / asset['uri'] for identity, asset in assets.items()}
    for checkpoint in checkpoints:
        folder = checkpoint.parent
        report = read(folder / 'batch-report.json')
        successful = {a['id']: a for a in read(folder / 'native-manifest.json')['assets']}
        authoritative = args.patches is not None and folder.parent == args.patches
        for identity, asset in successful.items():
            if identity not in assets or authoritative:
                assets[identity] = asset
                asset_sources[identity] = folder / asset['uri']
        failed = {e['input']: e.get('error', e.get('reason')) for kind in ['failed', 'missing', 'excluded'] for e in report[kind]}
        for plan in read(folder / 'plan.json')['models']:
            identity = plan['id']
            if authoritative and identity not in successful:
                assets.pop(identity, None)
                asset_sources.pop(identity, None)
            if identity not in assets: errors[identity] = failed.get(plan['input'], 'Batch produced no model')
    errors = {identity: reason for identity, reason in errors.items() if identity not in assets}
    for identity, asset in assets.items():
        source = asset_sources[identity]
        if source.parent == args.base: continue
        target = args.output / asset['uri']
        if target.exists() and sha(target) == sha(source): continue
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
    animation_issues = {}
    for identity, asset in assets.items():
        if not identity.startswith('wardrobe-') or not asset.get('clips'): continue
        gltf = read(asset_sources[identity])
        invalid = sorted({gltf['nodes'][channel['target']['node']]['name']
                          for clip in gltf.get('animations', []) for channel in clip['channels']
                          if not gltf['nodes'][channel['target']['node']].get('extras', {}).get('equipmentBone')})
        names = asset['clips']
        idle = [name for name in names if name.lower() == 'idle_a']
        if not idle: idle = [name for name in names if name.lower().endswith('_idle_a') and name.lower() != 'attack_idle_a' and not name.lower().endswith('_attack_idle_a')]
        if invalid:
            animation_issues[identity] = 'Source animation targets require attachment support: ' + ', '.join(invalid)
        elif len(names) > 1 and len(idle) != 1:
            animation_issues[identity] = 'Source animations have no unambiguous default idle sequence'
    baseline_missing = [list(k) for k in base_entries if k not in known]
    for entry in entries:
        key = (entry['itemId'], entry['bodyVariant'])
        source = sources[key]
        if source['customize'].get('capAttach') == '1' and all(
            c['attributes'].get('default', '0') == '0' for p in source['parts']
            for c in p['children'] if c['tag'] == 'userrotation'):
            # A free attachment with identity user rotation does not replace a
            # hair slot. Preserve the same hairstyle's ordinary A form.
            entry['hatHairForm'] = 'a'
            entry['blockers'] = [b for b in entry['blockers'] if b != 'Hat hair-form selection requires source fitting evidence']
            entry['limitations'].append('Free hat uses its authored attachment; user position/rotation controls are not exposed')
        rotations = [c['attributes'] for p in source['parts'] for c in p['children'] if c['tag'] == 'userrotation' and c['attributes'].get('default', '0') != '0']
        if rotations:
            entry['blockers'].append('Source user-rotation defaults require attachment control support: ' + str(rotations))
        if any(p['attributes'].get('earfold') == '1' for p in source['parts']):
            entry['limitations'].append('Source ear-folding behavior is not implemented')
        pony = [child for part in source['parts'] for child in part['children'] if child['tag'] in ['custom', 'jointangle']]
        if pony:
            entry['blockers'].append('Authored ponytail placement and joint-angle controls are not implemented; source custom/jointangle records are retained in the inventory')
        if any(child['tag'] == 'physx' for part in source['parts'] for child in part['children']):
            entry['limitations'].append('Authored physics behavior is not simulated')
        if set(entry['slots']) & {'RH', 'LH'} and not entry.get('handParts'):
            targets = [part['targetNode'] for part in source['parts']]
            if any('Back' in (target or '') or 'Side' in (target or '') for target in targets):
                entry['limitations'].append('Uses the authored stowed weapon anchor; drawn placement is not implemented for this source family')
        item_id = str(entry['itemId'])
        if entry['slots'] == ['FA'] and item_id in extra['faces']:
            entry['blockers'] = [b for b in entry['blockers'] if b != 'Face texture and expression sequence conversion pending']
        if entry['slots'] == ['FD'] and item_id in extra['makeup']:
            entry['blockers'] = [b for b in entry['blockers'] if b != 'Makeup texture, mask and placement conversion pending']
            entry['decal'] = extra['makeup'][item_id]
        if item_id in extra['failures']:
            entry['blockers'] = [b for b in entry['blockers'] if not b.endswith('conversion pending')]
            entry['blockers'].append(extra['failures'][item_id])
        old = base_entries.get(key)
        alias = inherited.get(entry['family'])
        if (old and old['availability'] != 'unavailable') or (alias and not entry['blockers']):
            source = old if old and old['availability'] != 'unavailable' else alias
            identity = {k: entry[k] for k in ['itemId', 'bodyVariant', 'sourceName', 'sourceIcon', 'isOutfit', 'classification', 'family', 'presetId']}
            entry.update(copy.deepcopy(source))
            entry.update(identity)
            if source is not old:
                entry.update(availability='preview', reason='Shared source bundle; this item identity has not been visually reviewed')
            entry['conversion'] = 'baseline-reused'
            entry['blockers'] = []
        else:
            required = [p['assetId'] for p in entry['parts']] + entry.get('stowedParts', [])
            required += [v for group in [entry.get('handParts', {}), entry.get('hairForms', {})] for ids in group.values() for v in ids]
            entry['blockers'] += [errors.get(i, 'Source family has not been converted in this checkpoint') for i in required if i not in assets]
            entry['blockers'] += [animation_issues[i] for i in required if i in animation_issues]
            if not required and not entry.get('decal') and not entry['blockers']: entry['blockers'].append('No usable geometry or decal')
            entry['blockers'] = sorted(set(entry['blockers']))
            entry.update(availability='unavailable' if entry['blockers'] else 'preview', reason='; '.join(entry['blockers']) or 'Converted source bundle; appearance has not been verified', conversion='failed' if any(i in errors and i not in assets for i in required) else 'customization-only' if entry.get('decal') and not required else 'converted' if required and all(i in assets for i in required) else 'not-converted')
        omissions = [assets[p['assetId']].get('omitted', []) for p in entry['parts'] if p['assetId'] in assets]
        if any(omissions):
            entry['limitations'].append('Source exporter omitted attached features; see native manifest omitted records')
        for note in visual_notes:
            if entry['family'] in note['families']: entry['limitations'].append(note['limitation'])
        entry['visualReview'] = 'baseline-reviewed' if entry['availability'] == 'verified' else 'unreviewed'
        if entry['availability'] == 'unavailable':
            # Discovery records do not need unusable or half-converted model IDs.
            entry['parts'] = []
            for field in ['handParts', 'hairForms', 'stowedParts']: entry.pop(field, None)
    manifest['assets'] = list(assets.values())
    write(args.output / 'native-manifest.json', manifest)
    write(args.output / 'simulator-catalog.json', {'version': 1, 'nativeManifestVersion': 1, 'items': entries})
    coverage = {'scopedItems': len({i['itemId'] for i in entries}), 'bodyPairs': len(entries), 'searchableEntries': len(entries),
                'families': len({i['family'] for i in entries}), 'convertedModels': len(assets), 'convertedFamilies': len({i['family'] for i in entries if i['availability'] != 'unavailable'}),
                'availability': dict(Counter(i['availability'] for i in entries)),
                'conversion': dict(Counter(i['conversion'] for i in entries)), 'baselineOnlyEntries': baseline_missing,
                'bySlotBody': {s: {b: dict(Counter(i['availability'] for i in entries if s in i['slots'] and i['bodyVariant'] == b)) for b in ['male', 'female']} for s in sorted({s for i in entries for s in i['slots']})}}
    write(args.output / 'coverage.json', coverage)
    compiler_records = []
    for path in sorted(Path('NifToGltf/obj/wardrobe').glob('patched-build*/bin/NifToGltf/debug/build-provenance.json')):
        record = read(path)
        if sha(Path(record['binary'])) != record['binaryHash']: raise ValueError('Converter binary changed: ' + record['binary'])
        compiler_records.append(record)
    write(args.output / 'wardrobe-provenance.json', {
        **read(args.work / 'provenance.json'),
        'baseline': {'directory': args.base.name, 'inventoryHash': sha(args.base / 'release-inventory.json')},
        'checkpoints': [{'directory': str(p.parent), 'sha256': sha(p), 'record': read(p)} for p in checkpoints],
        'customizationHash': sha(args.customization / 'metadata.json') if args.customization else None,
        'assemblySourceHash': sha(Path(__file__)),
        'visualNotesHash': sha(Path('NifToGltf/Diagnostics/wardrobe-visual-notes.json')),
        'textureAuditHash': sha(Path('NifToGltf/obj/wardrobe/texture-provenance-audit.json')),
        'matchingTextureArchiveHashes': sha(Path('NifToGltf/obj/wardrobe/matching-texture-hashes.json')),
        'compilerRecords': compiler_records,
        'sourceProvenanceNote': 'Matching binary/PDB hashes and compiler document checksums identify build sources. Earlier checkpoint converterSourceHashes fields are working-tree snapshots at checkpoint time, not compiler provenance, because versioned binaries ran while later sources were edited.',
        'appearanceScope': 'appearance-review.json is the unchanged inherited baseline geometry review. It does not accept this expanded catalog. New identities remain previews or unavailable; capture reviews are recorded separately.',
    })
    write(args.output / 'animation-compatibility.json', animation_issues)
    write(args.output / 'failure-families.json', [{'assetId': k, 'reason': v} for k, v in sorted(errors.items())])
    write(args.output / 'release-inventory.json', {'version': 1, 'files': [{'path': p.relative_to(args.output).as_posix(), 'bytes': p.stat().st_size, 'sha256': sha(p)} for p in sorted(args.output.rglob('*')) if p.is_file() and p.name != 'release-inventory.json']})
    print(json.dumps({k:v for k,v in coverage.items() if k!='bySlotBody'}, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=['prepare', 'run', 'assemble'])
    parser.add_argument('--inventory', type=Path, default=Path('NifToGltf/obj/wardrobe/wardrobe-inventory.json'))
    parser.add_argument('--resources', type=Path, default=Path('Maple2Storage/Resources'))
    parser.add_argument('--work', type=Path, default=Path('NifToGltf/obj/wardrobe/expansion'))
    parser.add_argument('--base', type=Path, default=Path('../MapleStory2-Handbook/static/gltf/simulator-release-05'))
    parser.add_argument('--output', type=Path)
    parser.add_argument('--customization', type=Path)
    parser.add_argument('--converter', type=Path)
    parser.add_argument('--patches', type=Path)
    parser.add_argument('--batches', type=int, default=1)
    parser.add_argument('--batch-size', type=int, default=40)
    parser.add_argument('--workers', type=int, choices=[1, 2], default=1)
    args = parser.parse_args()
    globals()[args.mode](args)
