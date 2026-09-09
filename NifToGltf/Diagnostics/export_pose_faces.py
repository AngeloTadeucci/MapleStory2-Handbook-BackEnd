"""Add client-authored pose face sequences to a local simulator candidate."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import xml.etree.ElementTree as ET


def sequence(emotion, code):
    frames = []
    for frame in emotion.findall("textureani[@target='FA']/texture"):
        def path(attribute):
            value = frame.get(attribute)
            return str(Path(value.replace('{0}', code).lower()).with_suffix('.json')).replace('\\', '/') if value else None
        frames.append({'image': path('file'), 'mask': path('control'),
                       'duration': int(frame.get('delay', '1000'))})
    if not frames:
        return None
    return {'frames': frames,
            'repeat': any(f.get('loop') == 'true' for f in emotion.findall("textureani[@target='FA']/texture")),
            'sourceAnimation': emotion.find('anim').get('name') if emotion.find('anim') is not None else None}


def export(xml, common, textures, release):
    path = release / 'customization.json'
    data = json.loads(path.read_text())
    names = json.loads(Path(__file__).with_name('character-animation-names.json').read_text())['emotes']
    sources = {}
    copied = set()
    report = {'faces': {}, 'missing': []}
    for face_id, preset in data['faces'].items():
        imp = ET.parse(xml / f'emotion/item/{face_id}.xml').getroot().find('import')
        source = imp.get('name').lower()
        if source not in sources:
            source_path = common / f'{source}.xml'
            if not source_path.exists():
                source_path = xml / f'emotion/common/{source}.xml'
            sources[source] = {e.get('name', '').lower(): e for e in ET.parse(source_path).getroot()}
        poses = {}
        for clip, name in names.items():
            emotion = sources[source].get(name['emotion'].lower())
            if emotion is None:
                continue
            value = sequence(emotion, preset['code'])
            if not value:
                continue
            if clip not in {a.get('name', '').lower() for a in emotion if a.tag in ('anim', 'idleAnim')}:
                continue
            paths = {f[k] for f in value['frames'] for k in ('image', 'mask') if f[k]}
            missing = [p for p in paths if not (release / 'faces' / p).exists() and not (textures / p).exists()]
            if missing:
                report['missing'].append({'face': face_id, 'clip': clip, 'paths': missing})
                continue
            for p in paths:
                target = release / 'faces' / p
                if not target.exists():
                    target.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(textures / p, target)
                copied.add('faces/' + p)
            poses[clip] = value
        preset['poseExpressions'] = poses
        report['faces'][face_id] = len(poses)
    for face_id in ['10300001', '10300003']:
        if report['faces'][face_id] != len(names):
            raise ValueError(f'Default face {face_id} has only {report["faces"][face_id]} authored pose expressions')
    path.write_text(json.dumps(data, indent=2) + '\n')
    report_path = release / 'pose-face-report.json'
    report_path.write_text(json.dumps(report, indent=2) + '\n')
    inventory_path = release / 'release-inventory.json'
    inventory = json.loads(inventory_path.read_text())
    entries = {e['path']: e for e in inventory['files']}
    for relative in copied | {'customization.json', 'pose-face-report.json'}:
        content = (release / relative).read_bytes()
        entries[relative] = {'path': relative, 'bytes': len(content), 'sha256': hashlib.sha256(content).hexdigest()}
    inventory['files'] = sorted(entries.values(), key=lambda e: e['path'])
    inventory_path.write_text(json.dumps(inventory, indent=2) + '\n')
    print(f'Exported pose faces for {len(report["faces"])} presets; {len(report["missing"])} missing sequences')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['xml', 'common', 'textures', 'release']:
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    export(args.xml, args.common, args.textures, args.release)
