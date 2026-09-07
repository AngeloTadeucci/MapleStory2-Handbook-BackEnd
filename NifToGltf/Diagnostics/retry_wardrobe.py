"""Retry one observed failure cause with an explicitly hashed converter binary."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
from expand_wardrobe import check_disk, read, sha
from wardrobe_inventory import digest, write


def retry(work, output, converter, match, resources, limit, textures_extra, include_patches, selection, generation, hair_inventory, textures_primary=None):
    if output.exists(): raise ValueError('Choose a fresh retry directory')
    plans = {}
    selected = set(read(selection)) if selection else None
    checkpoints = sorted(work.glob('batches/*/checkpoint.json'))
    if include_patches: checkpoints += sorted(output.parent.glob('*/checkpoint.json'))
    successes = set()
    for checkpoint in checkpoints:
        folder = checkpoint.parent
        report = read(folder/'batch-report.json')
        successes.update(a['id'] for a in read(folder/'native-manifest.json')['assets'])
        failed = {e['input'] for e in report['failed']+report['missing'] if match in e['error']}
        for plan in read(folder/'plan.json')['models']:
            if (selected is not None and plan['id'] in selected) or (selected is None and plan['input'] in failed): plans[plan['id']]=plan
    attempted = {m['id'] for p in output.parent.glob('*/checkpoint.json')
                 if (not include_patches and selected is None) or p.parent.name.rsplit('-',1)[0] == output.name.rsplit('-',1)[0]
                 for m in read(p.parent/'plan.json')['models']} | (successes if selected is None else set())
    plans = {k:v for k,v in plans.items() if k not in attempted}
    plans = dict(list(plans.items())[:limit])
    if hair_inventory:
        pairs = {(i['itemId'], i['bodyVariant']): i for i in read(hair_inventory)['items']}
        jobs = {j['plan']['id']: j for j in read(work/'jobs.json')}
        for identity, plan in plans.items():
            if plan.get('slot') == 'HR' and plan.get('attach') and not plan.get('itemModel'):
                item = pairs[tuple(jobs[identity]['items'][0])]
                primary = next(p for p in item['parts'] if p['slot'] == 'HR')
                plan.pop('attach')
                plan.update(itemModel='WardrobeSources/Xml/'+item['itemModel'], itemId=str(item['presetId']), alternateOf='WardrobeSources/'+primary['source'])
    if not plans: raise ValueError('No unattempted matching recorded failures')
    check_disk(work, len(plans)*32*1024**2)
    planpath = output.with_suffix('.plan.json')
    write(planpath, {'version':1,'models':list(plans.values())})
    result = subprocess.run(['dotnet',str(converter),'--native','--batch','--input',str(resources),'--textures',str(textures_primary or resources/'Models/Textures')+';'+str(resources/'WardrobeSources')+(';' + str(textures_extra) if textures_extra else ''),'--manifest',str(planpath),'--output',str(output)])
    if not (output/'batch-report.json').exists():raise RuntimeError('Retry produced no report')
    shutil.copy2(planpath,output/'plan.json')
    write(output/'checkpoint.json',{'match':match,'generation':generation,'selectionHash':sha(selection) if selection else None,'baseProvenanceHash':sha(work/'provenance.json'),'converterBinary':str(converter),'converterBinaryHash':sha(converter),'workingTreeSourceHashesAtCheckpoint':{p.as_posix():sha(p) for p in sorted(Path('NifToGltf/Native').glob('*.cs'))},'compilerProvenance':read(converter.with_name('build-provenance.json')) if converter.with_name('build-provenance.json').exists() else {'binaryHash':sha(converter),'pdbHash':sha(converter.with_suffix('.pdb'))},'primaryTextureProvenanceHash':sha(textures_primary/'archive-links.json') if textures_primary else None,'primaryTextureRoot':str(textures_primary) if textures_primary else None,'extraTextureHashes':{str(p):sha(p) for p in textures_extra.rglob('*.dds')} if textures_extra else {},'exitCode':result.returncode,'freeBytesAfter':check_disk(work)})


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--work',type=Path,default=Path('NifToGltf/obj/wardrobe/expansion'));p.add_argument('--output',type=Path,required=True);p.add_argument('--converter',type=Path,required=True);p.add_argument('--match',required=True);p.add_argument('--resources',type=Path,default=Path('Maple2Storage/Resources'));p.add_argument('--generation',type=int,default=1);p.add_argument('--hair-inventory',type=Path);p.add_argument('--selection',type=Path);p.add_argument('--include-patches',action='store_true');p.add_argument('--limit',type=int,default=64);p.add_argument('--textures-extra',type=Path);p.add_argument('--textures-primary',type=Path);a=p.parse_args();retry(a.work,a.output,a.converter,a.match,a.resources,a.limit,a.textures_extra,a.include_patches,a.selection,a.generation,a.hair_inventory,a.textures_primary)
