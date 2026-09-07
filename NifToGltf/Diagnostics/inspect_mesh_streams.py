"""Inspect exact vertex/index streams of a source NiMesh without conversion."""
import argparse
import json
import math
import struct
from inspect_nif import read_document, av_object
from scan_nif import Reader


def mesh_streams(path, block):
    strings, blocks, _ = read_document(path)
    kind, data = blocks[block]
    if kind != 'NiMesh': raise ValueError('Expected NiMesh')
    r = Reader(data)
    node = av_object(r, strings)
    materials = r.number()
    r.take(materials * 8 + 4 + 1)
    primitive, submeshes = r.number(), r.number('H')
    r.take(17)
    result = {}
    for _ in range(r.number()):
        target = r.number()
        r.take(1)
        regions = r.array('H', r.number('H'))
        semantics = [(strings[r.number()], r.number()) for _ in range(r.number())]
        stream = Reader(blocks[target][1])
        count = stream.number(); stream.number()
        ranges = [stream.array('I', 2) for _ in range(stream.number())]
        formats = stream.array('I', stream.number())
        payload = stream.take(count)
        stride = sum(((f >> 16) & 255) * ((f >> 8) & 255) for f in formats)
        offset = 0
        for (name, index), fmt in zip(semantics, formats):
            components = (fmt >> 16) & 255
            code = {0x00010215:'H', 0x00010425:'I', 0x00010435:'f', 0x00020436:'f', 0x00030437:'f', 0x00040438:'f', 0x00040108:'B', 0x00040110:'B', 0x00040214:'h'}[fmt]
            values = []
            for region in regions:
                start, length = ranges[region]
                values.append([list(struct.unpack_from('<'+code*components,payload,v*stride+offset)) for v in range(start,start+length)])
            result[name+':'+str(index)] = values
            offset += components * ((fmt >> 8) & 255)
    return {'node': node['name'], 'primitive': primitive, 'submeshes': submeshes, 'streams': result}


if __name__ == '__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('source');p.add_argument('block',type=int);p.add_argument('--output');a=p.parse_args()
    result=mesh_streams(a.source,a.block)
    streams=result['streams'];zeros=[]
    for name,submeshes in streams.items():
        if name.startswith('NORMAL'):
            for submesh,values in enumerate(submeshes):
                indices={v[0] for v in streams['INDEX:0'][submesh]}
                for index,normal in enumerate(values):
                    if sum(v*v for v in normal)<1e-16:
                        zeros.append({'semantic':name,'submesh':submesh,'index':index,'referenced':index in indices,
                                      'position':streams.get('POSITION_BP:0',streams.get('POSITION:0'))[submesh][index]})
    result['zeroNormals']=zeros
    if a.output:
        with open(a.output,'w') as f:json.dump(result,f)
    print(json.dumps({k:v for k,v in result.items() if k!='streams'},indent=2))
