"""Compare native tracks at Noesis key times, by exact source node name.

This measures reference agreement, not correctness of the source NIF curves.
Only self-contained or relative-buffer glTFs with float32 LINEAR/STEP tracks
are supported. No third-party Python dependencies are required.
"""

import argparse
import base64
from bisect import bisect_right
import json
import math
from pathlib import Path
import struct


def normalize(values):
    length = math.sqrt(sum(v * v for v in values))
    return [v / length for v in values]


def slerp(left, right, t):
    left, right = normalize(left), normalize(right)
    dot = sum(a * b for a, b in zip(left, right))
    if dot < 0:
        right, dot = [-v for v in right], -dot
    if dot > 0.9995:
        return normalize([a + (b - a) * t for a, b in zip(left, right)])
    angle = math.acos(min(1, dot))
    return [(a * math.sin((1 - t) * angle) + b * math.sin(t * angle)) / math.sin(angle)
            for a, b in zip(left, right)]


def load(path):
    document = json.loads(path.read_text(encoding='utf-8'))
    buffers = [base64.b64decode(b['uri'].split(',')[1]) if b['uri'].startswith('data:')
               else (path.parent / b['uri']).read_bytes() for b in document['buffers']]

    def accessor(index):
        item = document['accessors'][index]
        assert item['componentType'] == 5126 and 'sparse' not in item
        width = {'SCALAR': 1, 'VEC3': 3, 'VEC4': 4}[item['type']]
        view = document['bufferViews'][item['bufferView']]
        offset = view.get('byteOffset', 0) + item.get('byteOffset', 0)
        stride = view.get('byteStride', width * 4)
        return [struct.unpack_from('<' + 'f' * width, buffers[view['buffer']], offset + i * stride)
                for i in range(item['count'])]

    clips = {}
    for animation in document.get('animations', []):
        tracks = {}
        for channel in animation['channels']:
            target = channel['target']
            key = (document['nodes'][target['node']]['name'], target['path'])
            assert key not in tracks, f'Ambiguous track: {key}'
            sampler = animation['samplers'][channel['sampler']]
            mode = sampler.get('interpolation', 'LINEAR')
            assert mode in ('LINEAR', 'STEP')
            tracks[key] = ([v[0] for v in accessor(sampler['input'])], accessor(sampler['output']), mode)
        clips[animation['name'].lower()] = tracks
    return clips


def sample(track, time, rotation):
    times, values, mode = track
    index = max(0, min(len(times) - 1, bisect_right(times, time) - 1))
    if index == len(times) - 1 or mode == 'STEP':
        return values[index]
    t = max(0, min(1, (time - times[index]) / (times[index + 1] - times[index])))
    if rotation:
        return slerp(values[index], values[index + 1], t)
    return [a + (b - a) * t for a, b in zip(values[index], values[index + 1])]


def compare(native_path, reference_directory):
    report = []
    references = {path.stem.lower(): path for path in reference_directory.glob('*.gltf')}
    for name, tracks in load(native_path).items():
        reference = load(references[name])
        assert len(reference) == 1
        expected = next(iter(reference.values()))
        errors = {'rotation_degrees': 0, 'translation_source_units': 0, 'scale': 0}
        worst = {}
        unmatched = sorted(set(expected) - set(tracks))
        for key in set(expected) & set(tracks):
            is_rotation = key[1] == 'rotation'
            category = 'rotation_degrees' if is_rotation else 'translation_source_units' if key[1] == 'translation' else 'scale'
            for time, value in zip(expected[key][0], expected[key][1]):
                actual = sample(tracks[key], time, is_rotation)
                if is_rotation:
                    dot = abs(sum(a * b for a, b in zip(normalize(actual), normalize(value))))
                    error = 2 * math.degrees(math.acos(min(1, dot)))
                else:
                    error = max(abs(a - b) for a, b in zip(actual, value))
                if error > errors[category]:
                    errors[category] = error
                    worst[category] = {'node': key[0], 'time': time}
        report.append({'clip': name, 'compared_tracks': len(set(expected) & set(tracks)),
                       'missing_native_tracks': unmatched, 'maximum_error': errors, 'worst_sample': worst})
    return {'method': 'Native interpolation evaluated at Noesis keys; transforms remain in source coordinates.', 'clips': report}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('native', type=Path)
    parser.add_argument('reference_directory', type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    result = compare(args.native, args.reference_directory)
    args.output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(result, indent=2))
