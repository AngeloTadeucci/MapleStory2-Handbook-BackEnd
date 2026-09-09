"""Read NIF block payloads for conversion diagnostics, without changing sources."""
from pathlib import Path
import argparse
import json
from scan_nif import Reader


def read_document(path):
    data = Path(path).read_bytes()
    r = Reader(data)
    header = bytes(r.take(data.index(b'\n') + 1)).rstrip(b'\n')
    older_version = header == b'Gamebryo File Format, Version 30.1.0.3'
    if header != b'Gamebryo File Format, Version 30.2.0.3' and not older_version:
        raise ValueError('Expected NIF 30.2.0.3 or 30.1.0.3')
    if r.number() != (0x1E010003 if older_version else 0x1E020003) or r.number('B') != 1:
        raise ValueError('Unsupported NIF version/endian')
    r.number()
    count = r.number()
    r.take(r.number())
    types = [r.string() for _ in range(r.number('H'))]
    indices = r.array('H', count)
    sizes = r.array('I', count)
    strings_count = r.number()
    r.number()
    strings = [r.string() for _ in range(strings_count)]
    r.array('I', r.number())
    blocks = [(types[i & 0x7fff], bytes(r.take(size))) for i, size in zip(indices, sizes)]
    roots = r.array('i', r.number())
    r.finish()
    return strings, blocks, roots


def object_net(r, strings):
    index = r.number()
    name = strings[index] if index != 0xffffffff else ''
    extras = r.array('i', r.number())
    controller = r.number('i')
    return name, extras, controller


def av_object(r, strings):
    name, extras, controller = object_net(r, strings)
    flags = r.number('H')
    transform = r.array('f', 13)
    properties = r.array('i', r.number())
    collision = r.number('i')
    return dict(name=name, flags=flags, transform=transform, properties=properties,
                controller=controller, extras=extras, collision=collision)


def inspect(path):
    strings, blocks, roots = read_document(path)
    result = dict(roots=roots, strings=strings, blocks=[])
    for i, (kind, data) in enumerate(blocks):
        record = dict(id=i, type=kind, bytes=len(data))
        r = Reader(data)
        if kind in ('NiNode', 'NiSortAdjustNode', 'NiBillboardNode', 'NiMesh',
                    'NiTextureEffect', 'NiCamera', 'NiPSParticleSystem', 'NiPSMeshParticleSystem'):
            record.update(av_object(r, strings))
            if kind in ('NiNode', 'NiSortAdjustNode', 'NiBillboardNode'):
                record['children'] = r.array('i', r.number())
                record['effects'] = r.array('i', r.number())
            if kind == 'NiTextureEffect':
                record['enabled'] = r.number('B')
                record['affected'] = r.array('i', r.number())
                record['projection'] = r.array('f', 12)
                record['filter'] = r.number()
                record['anisotropy'] = r.number('H')
                record['clamp'], record['textureType'], record['coordinateGeneration'] = r.array('I', 3)
                record['texture'] = r.number('i')
        elif kind == 'NiSourceTexture':
            record['name'], _, _ = object_net(r, strings)
            record['external'] = r.number('B')
            index = r.number()
            record['filename'] = strings[index] if index != 0xffffffff else ''
            record['pixels'] = r.number('i')
        elif kind == 'NiSequenceData':
            record['name'] = strings[r.number()]
            record['evaluators'] = r.array('i', r.number())
            record['textKeys'] = r.number('i')
            record['duration'] = r.number('f')
        record['remainingHex'] = bytes(r.take(len(data)-r.offset)).hex() if not kind.startswith('NiDataStream') else ''
        result['blocks'].append(record)
    return result


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('input')
    p.add_argument('--output')
    args = p.parse_args()
    value = json.dumps(inspect(args.input), indent=2)
    if args.output:
        Path(args.output).write_text(value, encoding='utf-8')
    else:
        print(value)
