"""Export the client hair-twinkle pilot, including source mesh emission surfaces.

This deliberately accepts one inspected NIF revision, not arbitrary effects.
Unknown revisions must be reviewed before extending the runtime contract.
"""
import argparse
import hashlib
import json
from pathlib import Path
import struct
import xml.etree.ElementTree as ET
from inspect_nif import read_document, av_object, object_net
from scan_nif import Reader


def export_effect(nif, definition, items):
    if hashlib.sha256(nif.read_bytes()).hexdigest() != '34a815539e159876cdee93cdaf19bf4dc9838bc58b33986253f08aee21072a97':
        raise ValueError('Unreviewed hair-twinkle NIF revision')
    strings, blocks, roots = read_document(nif)
    def reader(index, kind):
        actual, data = blocks[index]
        if actual != kind:
            raise ValueError(f'Expected {kind} at {index}, found {actual}')
        return Reader(data)
    nodes, parents = {}, {}
    for i, (kind, data) in enumerate(blocks):
        if kind in ('NiNode', 'NiBillboardNode', 'NiMesh', 'NiPSParticleSystem'):
            r = Reader(data)
            node = av_object(r, strings)
            node['payload'] = bytes(r.take(len(data) - r.offset))
            nodes[i] = node
            if kind in ('NiNode', 'NiBillboardNode'):
                r = Reader(node['payload'])
                for child in r.array('i', r.number()):
                    parents[child] = i
    def point(index, value, normal=False):
        t = nodes[index]['transform']
        result = [sum(t[3 + row * 3 + col] * value[col] for col in range(3)) * (1 if normal else t[12]) + (0 if normal else t[row]) for row in range(3)]
        return point(parents[index], result, normal) if index in parents else result
    def mesh(index, local=False):
        r = Reader(nodes[index]['payload'])
        count = r.number()
        r.array('I', count * 2)
        r.number(); r.number('B')
        primitive = r.number(); submeshes = r.number('H'); r.number('B')
        r.array('f', 4)
        if primitive != 0 or submeshes != 1:
            raise ValueError('Pilot requires one triangle submesh')
        attributes = {}
        for _ in range(r.number()):
            stream = r.number('i'); r.number('B')
            regions = r.array('H', r.number('H'))
            semantics = [(strings[r.number()], r.number()) for _ in range(r.number())]
            s = Reader(blocks[stream][1]); size = s.number(); s.number()
            ranges = [s.array('I', 2) for _ in range(s.number())]
            formats = s.array('I', s.number()); raw = s.take(size); s.number('B'); s.finish()
            stride = sum(((f >> 16) & 255) * ((f >> 8) & 255) for f in formats)
            offset = 0
            for (name, channel), f in zip(semantics, formats):
                width, byte_width = (f >> 16) & 255, (f >> 8) & 255
                if f not in (0x10215, 0x20436, 0x30437):
                    raise ValueError(f'Unsupported effect stream {f:x}')
                start, length = ranges[regions[0]]
                values = [list(struct.unpack_from('<' + ('H' if byte_width == 2 else 'f') * width, raw, (start + v) * stride + offset)) for v in range(length)]
                attributes[f'{name}_{channel}'] = values
                offset += width * byte_width
        if r.number() != 0:
            raise ValueError('Emitter geometry has modifiers')
        r.finish()
        return dict(positions=attributes['POSITION_0'] if local else [point(index, v) for v in attributes['POSITION_0']],
                    normals=[point(index, v, True) for v in attributes['NORMAL_0']],
                    indices=[v[0] for v in attributes['INDEX_0']],
                    uv=attributes.get('TEXCOORD_0', []))
    def material(index):
        r = reader(index, 'NiMaterialProperty'); object_net(r, strings)
        result = dict(zip(['ambient', 'diffuse', 'specular', 'emissive'], [r.array('f', 3) for _ in range(4)]))
        result['power'] = r.number('f'); result['alpha'] = r.number('f'); r.finish()
        return result
    def curve(index):
        r = reader(index, 'NiFloatData'); count = r.number()
        if r.number() != 2:
            raise ValueError('Expected quadratic float keys')
        values = [r.array('f', 4) for _ in range(count)]; r.finish()
        return values
    textures = []
    r = reader(42, 'NiTexturingProperty'); object_net(r, strings); r.number('H')
    for slot in range(r.number()):
        if not r.number('B'):
            continue
        source = r.number('i'); flags = r.number('H'); r.number('H')
        transform = None
        if r.number('B'):
            transform = dict(translation=r.array('f', 2), scale=r.array('f', 2), rotation=r.number('f'), method=r.number(), center=r.array('f', 2))
        s = reader(source, 'NiSourceTexture'); object_net(s, strings); s.number('B')
        textures.append(dict(slot=slot, texture=Path(strings[s.number()]).with_suffix('.png').name, flags=flags, transform=transform))
    if r.number() != 0:
        raise ValueError('Shader textures not supported')
    r.finish()
    systems = []
    for system_id, emitter_id, general_id, rate_id, material_id in [(7, 36, 27, 13, 25), (75, 89, 83, 78, 81)]:
        r = reader(emitter_id, 'NiPSMeshEmitter'); name = strings[r.number()]
        keys = ['speed', 'speedVariation', 'speedFlip', 'declination', 'declinationVariation', 'planarAngle', 'planarVariation', 'size', 'sizeVariation', 'lifespan', 'lifespanVariation', 'rotation', 'rotationVariation', 'rotationSpeed', 'rotationSpeedVariation']
        emitter = dict(zip(keys, r.array('f', len(keys))))
        emitter['rotationAxis'] = r.array('f', 3)
        emitter['randomRotationSign'] = bool(r.number('B')); emitter['randomRotationAxis'] = bool(r.number('B'))
        surfaces = r.array('i', r.number()); emitter_object = r.number('i')
        emission = r.number(); velocity = r.number(); r.finish()
        if len(surfaces) != 1 or emitter_object != -1 or emission != 3 or velocity != 0:
            raise ValueError('Pilot requires face-surface emission with mesh normals')
        emitter['surface'] = mesh(surfaces[0])
        g = reader(general_id, 'NiPSSimulatorGeneralStep')
        for _ in range(3):
            if g.number('B') != 0 or g.number() != 2:
                raise ValueError('Unexpected particle age keys')
        emitter['growTime'], emitter['shrinkTime'] = g.array('f', 2)
        if g.array('H', 2) != [0, 0]:
            raise ValueError('Unexpected particle generation')
        g.finish()
        rate = reader(rate_id, 'NiFloatInterpolator'); emitter['rate'] = rate.number('f')
        if rate.number('i') != -1:
            raise ValueError('Animated birth rates are not supported')
        rate.finish()
        # These source systems use one-second looping emission, camera-facing
        # quads, no rotations/colors/spawners, and capacities five and three.
        emitter.update(name=nodes[system_id]['name'], material=material(material_id),
                       capacity=5 if system_id == 7 else 3, worldSpace=system_id == 7,
                       drag=0.1 if system_id == 7 else 0, texture='hitlight_8-2.png')
        systems.append(emitter)
    effect = ET.parse(definition).getroot().find('effect')
    attach = effect.find('attachInfo')
    transform = effect.find('transform')
    if effect.get('filePath').lower() != 'item/hair/eff_hair_twinkle_a.nif' or effect.get('loop') != 'true' or attach.attrib != {'attachToNode':'true','applyNodeTransform':'true','parentNode':'Bip01 Head'} or transform.attrib != {'translate': '0.0, 0.0, 0.0', 'rotation': '0.0, 0.0, 0.0', 'scale': '1.0'}:
        raise ValueError('Unexpected effect attachment contract')
    item_ids = []
    for item in ET.parse(items).getroot():
        for e in item.findall('environment/effect'):
            idle = e.get('characterIdle', '').replace('\\', '/').lower()
            battle = e.get('characterBattleIdle', '').replace('\\', '/').lower()
            if idle == battle == 'item/hair/eff_hair_twinkle_a.xml':
                item_ids.append(int(item.get('id')))
    return dict(version=1, kind='hair-twinkle-a', attachNode='Bip01 Head', itemIds=item_ids,
                sources={p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in [nif, definition, items]},
                systems=systems, glow=dict(surface=mesh(71, True), material=material(53), textures=textures,
                    transforms=[nodes[i]['transform'] for i in [37, 69, 71]],
                    alphaKeys=curve(56), scaleUKeys=curve(46), scaleVKeys=curve(48)),
                notes=['Preview: particle random sequence, drag integration and glow material parity have not been matched to the running client.'])


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--sources', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    args = p.parse_args()
    if args.output.exists():
        raise ValueError('Choose a fresh effect output')
    data = export_effect(args.sources / 'Models/item/hair/eff_hair_twinkle_a.nif',
                         args.sources / 'Definitions/effect/item/hair/eff_hair_twinkle_a.xml',
                         args.sources / 'Definitions/itemdata/102.xml')
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(data, indent=2), encoding='utf-8')
    print(f'Exported {len(data["systems"])} systems for {len(data["itemIds"])} source hair IDs')
