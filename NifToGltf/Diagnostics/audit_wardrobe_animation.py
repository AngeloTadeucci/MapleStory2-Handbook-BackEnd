"""Identify source controller requirements and duplicate exported body targets."""
import argparse
from collections import Counter
from pathlib import Path
import struct
from inspect_nif import read_document
from expand_wardrobe import read, sha
from wardrobe_inventory import write


def audit(library, resources, output):
    sources, records = {}, []
    for asset in read(library/'native-manifest.json')['assets']:
        if not asset['id'].startswith('wardrobe-'): continue
        source = resources/asset['input']
        if source not in sources:
            strings, blocks, roots = read_document(source)
            controllers = []
            for index, (kind, data) in enumerate(blocks):
                if ('Controller' not in kind and not kind.endswith('Ctlr')) or len(data) < 26: continue
                _, flags, frequency, phase, start, stop, target = struct.unpack_from('<iHffffi', data)
                if not flags & 8 or stop <= start: continue
                if kind == 'NiTransformController' and len(data) == 30:
                    interpolator = struct.unpack_from('<i', data, 26)[0]
                    if interpolator >= 0 and blocks[interpolator][0] == 'NiTransformInterpolator' and struct.unpack_from('<i', blocks[interpolator][1],32)[0] < 0: continue
                controllers.append({'block':index,'type':kind,'flags':flags,'frequency':frequency,'phase':phase,'start':start,'stop':stop,'target':target})
            sources[source] = {'path':str(source),'sha256':sha(source),'controllers':controllers}
        gltf = read(library/asset['uri'])
        names = Counter(n.get('name') for n in gltf['nodes'])
        duplicate = names.get('Bip01',0)>1
        if duplicate or sources[source]['controllers']:
            records.append({'assetId':asset['id'],'source':str(source),'duplicateBodyTargets':duplicate,'controllers':sources[source]['controllers']})
    write(output, {'libraryHash':sha(library/'release-inventory.json'),'sources':list(sources.values()),'records':records})
    write(output.with_suffix('.ids.json'), [r['assetId'] for r in records])
    print({'auditedSources':len(sources),'selectedModels':len(records),'controllerModels':sum(bool(r['controllers']) for r in records),'duplicateBodyModels':sum(r['duplicateBodyTargets'] for r in records)})

if __name__ == '__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--library',type=Path,required=True);p.add_argument('--resources',type=Path,default=Path('Maple2Storage/Resources'));p.add_argument('--output',type=Path,required=True);a=p.parse_args();audit(a.library,a.resources,a.output)
