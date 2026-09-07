"""Export source face sequences and fixed makeup defaults for wardrobe candidates.

Separate geometry and customization checkpoints. Unknown masks/transforms remain
explicit blockers. Existing baseline files and private snapshots are never edited.
"""
import argparse
import json
from pathlib import Path
import shutil
import subprocess
import xml.etree.ElementTree as ET
from wardrobe_inventory import write


def prepare(inventory, xml, textures, output):
    if output.exists(): raise ValueError('Choose a fresh customization work directory')
    rows = json.loads(inventory.read_text())['items']
    models = {int(e.get('id')):e for p in (xml/'itemmodel').glob('*.xml') for e in ET.parse(p).getroot()}
    faces, makeup, failures, required = {}, {}, {}, set()
    for row in rows:
        key = str(row['itemId'])
        if key in faces or key in makeup or key in failures: continue
        if row['slots'] == ['FA']:
            path = xml / 'emotion/item' / (key+'.xml')
            if not path.exists():
                failures[key] = 'Missing source emotion import: '+str(path.relative_to(xml)); continue
            imp = ET.parse(path).getroot().find('import')
            if imp is None:
                failures[key] = 'Face has no supported emotion import'; continue
            common = xml/'emotion/common'/(imp.get('name').lower()+'.xml')
            if not common.exists():
                failures[key] = 'Missing source emotion common: '+str(common.relative_to(xml)); continue
            sequences = {}
            for name in ['default','happy','angry','sad']:
                emotion = ET.parse(common).getroot().find(f"emotion[@name='{name}']")
                if emotion is None: continue
                frames=[]
                for frame in emotion.findall("textureani[@target='FA']/texture"):
                    def relative(attribute):
                        value=frame.get(attribute)
                        return value.replace('{0}',imp.get('itemCode', '')).replace('\\','/').lower() if value else None
                    image,mask=relative('file'),relative('control')
                    if not image or not (textures/image).is_file() or (mask and not (textures/mask).is_file()):
                        failures[key]='Missing source face texture: '+str(image if not image or not (textures/image).is_file() else mask);break
                    required.add(image)
                    if mask:required.add(mask)
                    frames.append({'image':str(Path(image).with_suffix('.json')),'mask':str(Path(mask).with_suffix('.json')) if mask else None,'duration':int(frame.get('delay','1000'))})
                if frames:sequences[name]={'frames':frames,'repeat':name=='default','sourceAnimation':emotion.find('anim').get('name') if emotion.find('anim') is not None else None}
            if 'default' not in sequences:
                failures.setdefault(key,'No complete default face sequence');continue
            if key not in failures:faces[key]={'code':imp.get('itemCode', ''),'sequences':sequences}
        if row['slots']==['FD']:
            model=models[row['presetId']]; slot=model.find("slots/slot[@name='FD']"); decal=slot.find('decal');scale=slot.find('scale')
            if decal is None or scale is None:
                failures[key]='Makeup has no decal/scale definition';continue
            customs=decal.findall('custom')
            if not customs:
                failures[key]='Makeup has no authored placement';continue
            custom=customs[0];position=[float(v) for v in custom.get('position','0,0,0').split(',')];rotation=[float(v) for v in custom.get('rotation','0,0,0').split(',')]
            if position[2] or rotation[0] or rotation[1]:
                failures[key]='Makeup has a nonplanar source transform';continue
            relative=decal.get('texture').replace('\\','/').lower().removeprefix('./data/resource/model/textures/')
            if not (textures/relative).is_file():
                failures[key]='Missing source makeup texture: '+relative;continue
            mask=decal.get('controltexture', '').replace('\\','/').lower().removeprefix('./data/resource/model/textures/')
            if mask and not (textures/mask).is_file():
                failures[key]='Missing source makeup dye mask: '+mask;continue
            if mask: required.add(mask)
            required.add(relative)
            makeup[key]={'texture':'makeup/'+str(Path(relative).with_suffix('.png')), 'transform':[position[0],position[1],rotation[2],float(scale.get('value').split(',')[0])],
                         'placements': [[*[float(v) for v in c.get('position','0,0,0').split(',')][:2],float(c.get('rotation','0,0,0').split(',')[2]),float(scale.get('value').split(',')[0])] for c in customs],
                         'scaleRange':[float(scale.get('min',scale.get('value'))),float(scale.get('max',scale.get('value')))]}
            if mask: makeup[key]['mask']='makeup/'+str(Path(mask).with_suffix('.png'))
    source=output/'source'
    for name in sorted(required):
        dest=source/name;dest.parent.mkdir(parents=True,exist_ok=True);dest.symlink_to((textures/name).resolve())
    write(output/'metadata.json',{'faces':faces,'makeup':makeup,'failures':failures,'sourceTextures':sorted(required)})
    subprocess.run(['dotnet','run','--no-build','--project','NifToGltf','--','--native','--texture-batch','--input',str(source),'--output',str(output/'textures')],check=True)
    print(json.dumps({'faces':len(faces),'makeup':len(makeup),'failures':failures,'textures':len(required)}))


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--inventory',type=Path,default=Path('NifToGltf/obj/wardrobe/wardrobe-inventory.json'))
    p.add_argument('--xml',type=Path,default=Path('Maple2Storage/Resources/SimulatorSources/Xml'))
    p.add_argument('--textures',type=Path,default=Path('Maple2Storage/Resources/Models/Textures'))
    p.add_argument('--output',type=Path,required=True)
    a=p.parse_args();prepare(a.inventory,a.xml,a.textures,a.output)
