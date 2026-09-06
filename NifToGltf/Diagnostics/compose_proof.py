"""Combine native self-contained glTFs for local attachment inspection only."""

import argparse
import copy
import json
from pathlib import Path


def compose(paths):
    arrays = ['nodes', 'meshes', 'skins', 'accessors', 'bufferViews', 'buffers',
              'materials', 'textures', 'images', 'samplers', 'animations']
    output = {key: [] for key in arrays}
    output.update(asset={'version': '2.0', 'generator': 'Native NIF attachment proof'},
                  scene=0, scenes=[{'nodes': []}])
    for path in paths:
        source = json.loads(path.read_text(encoding='utf-8'))
        if source.get('extensionsRequired'):
            raise ValueError('This proof composer does not handle required extensions')
        offset = {key: len(output[key]) for key in arrays}
        def shift(record, field, array):
            if field in record:
                record[field] += offset[array]
        for node in source['nodes']:
            shift(node, 'mesh', 'meshes')
            shift(node, 'skin', 'skins')
            if 'children' in node:
                node['children'] = [value + offset['nodes'] for value in node['children']]
        for mesh in source['meshes']:
            for primitive in mesh['primitives']:
                primitive['attributes'] = {key: value + offset['accessors']
                                           for key, value in primitive['attributes'].items()}
                shift(primitive, 'indices', 'accessors')
                shift(primitive, 'material', 'materials')
        for skin in source.get('skins', []):
            shift(skin, 'skeleton', 'nodes')
            shift(skin, 'inverseBindMatrices', 'accessors')
            skin['joints'] = [value + offset['nodes'] for value in skin['joints']]
        for accessor in source['accessors']:
            shift(accessor, 'bufferView', 'bufferViews')
        for view in source['bufferViews']:
            shift(view, 'buffer', 'buffers')
        for texture in source.get('textures', []):
            shift(texture, 'source', 'images')
            shift(texture, 'sampler', 'samplers')
        for material in source.get('materials', []):
            holders = [material, material.get('pbrMetallicRoughness', {})]
            for holder in holders:
                for key, info in holder.items():
                    if key.endswith('Texture') and isinstance(info, dict):
                        shift(info, 'index', 'textures')
            for info in material.get('extras', {}).get('nifTextures', {}).values():
                shift(info, 'index', 'textures')
        for animation in source.get('animations', []):
            for sampler in animation['samplers']:
                shift(sampler, 'input', 'accessors')
                shift(sampler, 'output', 'accessors')
            for channel in animation['channels']:
                shift(channel['target'], 'node', 'nodes')
        for root in source['scenes'][source.get('scene', 0)]['nodes']:
            output['scenes'][0]['nodes'].append(root + offset['nodes'])
        for key in arrays:
            if key != 'animations':
                output[key].extend(copy.deepcopy(source.get(key, [])))
        for animation in source.get('animations', []):
            existing = next((clip for clip in output['animations']
                             if clip.get('name') == animation.get('name')), None)
            if existing is None:
                output['animations'].append(copy.deepcopy(animation))
            else:
                for channel in animation['channels']:
                    channel['sampler'] += len(existing['samplers'])
                existing['channels'].extend(animation['channels'])
                existing['samplers'].extend(animation['samplers'])
    return {key: value for key, value in output.items() if value != []}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('models', nargs='+', type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    args.output.write_text(json.dumps(compose(args.models)), encoding='utf-8')
