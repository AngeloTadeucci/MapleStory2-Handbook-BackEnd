"""Run the local client solver against browser-captured Sassy head transforms.

This is an explicit fixed-step reference experiment. It does not claim the
client's scene scheduler, collisions with the character, or scene settings.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import struct
import subprocess


def multiply(a, b):
    r = [sum(a[i*3+k]*b[k*3+j] for k in range(3)) for i in range(3) for j in range(3)]
    return r + [sum(a[i*3+k]*b[9+k] for k in range(3))+a[9+i] for i in range(3)]


def inverse(a):
    r = [a[j*3+i] for i in range(3) for j in range(3)]
    return r + [-sum(r[i*3+k]*a[9+k] for k in range(3)) for i in range(3)]


def prepare_input(capture, source):
    if capture.get('error') or capture['running'] or capture['itemId'] != 10200010:
        raise ValueError('Invalid Sassy trajectory capture')
    if capture['placements'] != [0,0] or capture['scale'] != 1 or capture['bodyVariant'] != 'female':
        raise ValueError('Only the default female Sassy placement and size are supported')
    frames = capture['frames']; dt = capture['dt']
    if not 2 <= len(frames) <= 36000 or not .001 <= dt <= .1:
        raise ValueError('Trajectory bounds')
    actors = [b for b in source['blocks'] if b['type'] == 'NiPhysXActorDesc']
    placements = [multiply(tail[0], inverse(actors[0]['poses'][0])) for tail in capture['rest']]
    rest_errors = [max(abs(a-b) for a,b in zip(multiply(p,actor['poses'][0]),rest))
                   for p,tail in zip(placements,capture['rest']) for actor,rest in zip(actors,tail)]
    if len(placements) != 2 or max(rest_errors) > 1e-5:
        raise ValueError(f'Exported bone and source actor rest transforms differ: {rest_errors}')
    matrices = placements + [m for frame in frames for m in frame]
    if any(len(frame)!=2 for frame in frames) or any(len(m)!=12 or not all(math.isfinite(v) for v in m) for m in matrices):
        raise ValueError('Invalid trajectory matrix')
    # Browser/glTF is Y-up in meters. This gravity is an explicit experiment.
    data = b'PHXHAIR1' + struct.pack('<IIf3f',len(frames),2,dt,0,-9.8,0)
    data += b''.join(struct.pack('<12f',*m) for m in matrices)
    return data, rest_errors


def bake(capture_path, scene, dlls, output):
    capture=json.loads(capture_path.read_text(encoding='utf-8'))
    source=json.loads((scene/'source.json').read_text(encoding='utf-8'))
    data,rest_errors=prepare_input(capture,source)
    request=scene/'playback-input.bin';request.write_bytes(data)
    exe=scene/'simulate_hair284.exe'
    process=subprocess.run([str(exe),str(dlls),str(scene),str(request)],capture_output=True,text=True,timeout=60)
    if process.returncode or process.stderr.strip():
        raise ValueError(f'Native simulation failed: {process.returncode}: {process.stderr}')
    result=json.loads(process.stdout)
    if len(result['frames'])!=len(capture['frames']) or not result['clientCoreVerified']:
        raise ValueError('Native result does not match request')
    root_error=0
    for targets,frame in zip(capture['frames'],result['frames']):
        if len(frame)!=2 or any(len(tail)!=3 for tail in frame):raise ValueError('Invalid result bones')
        for target,tail in zip(targets,frame):
            for p in tail:
                if len(p)!=7 or not all(math.isfinite(x) for x in p) or abs(sum(x*x for x in p[3:])-1)>1e-4:
                    raise ValueError('Invalid simulated pose')
            root_error=max(root_error,max(abs(a-b) for a,b in zip(target[9:],tail[0][:3])))
    if root_error>1e-5:raise ValueError('Kinematic root did not follow the sampled head')
    result.update(version=1,itemId=10200010,bodyVariant='female',clip=capture['clip'],placements=[0,0],scale=1,
                  sourceSha256=source['sha256'],duration=(len(result['frames'])-1)*result['dt'],
                  verification={'restTransformMaxError':max(rest_errors),'rootPositionMaxError':root_error},
                  provenance={'coreSha256':hashlib.sha256((dlls/'PhysXCore64.dll').read_bytes()).hexdigest(),
                              'inputSha256':hashlib.sha256(data).hexdigest(),
                              'executableSha256':hashlib.sha256(exe.read_bytes()).hexdigest()},
                  limitations=['Client solver with experimental fixed 60 Hz timing and gravity 9.8 m/s2.',
                               'No character or hat collision shapes in this sample.',
                               'Default tail placements and size 1 only. Stops after eight seconds.',
                               'Client scene settings and update order are not verified. No client visual parity.'])
    output.parent.mkdir(parents=True,exist_ok=True)
    output.write_text(json.dumps(result,separators=(',',':'))+'\n',encoding='utf-8')
    return result


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('capture',type=Path);p.add_argument('scene',type=Path)
    p.add_argument('dlls',type=Path);p.add_argument('output',type=Path)
    a=p.parse_args();r=bake(a.capture,a.scene,a.dlls,a.output)
    print(json.dumps({'frames':len(r['frames']),'verification':r['verification']}))
