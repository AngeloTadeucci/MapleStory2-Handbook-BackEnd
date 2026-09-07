"""Read NIF 30.2.0.3 hair physics without modifying source or release assets.

Layouts follow the local NifSkope nif.xml. Drives retain their raw parameters:
the old schema's drive labels need confirmation before a solver consumes them.
This is a diagnostic decoder, not a physics implementation.
"""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path

from inspect_nif import read_document, av_object, object_net
from scan_nif import Reader


def decode(kind, data, strings):
    r = Reader(data)
    def name():
        index = r.number()
        return strings[index] if index != 0xffffffff else ''
    def vector():
        return r.array('f', 3)
    if kind == 'NiPhysXProp':
        prop_name, extras, controller = object_net(r, strings)
        value = dict(name=prop_name, extras=extras, controller=controller,
                     physXToWorldScale=r.number('f'), sources=r.array('i', r.number()),
                     destinations=r.array('i', r.number()), modifiedMeshes=r.array('i', r.number()),
                     keepMeshes=r.number('B'), description=r.number('i'))
    elif kind == 'NiPhysXPropDesc':
        value = dict(actors=r.array('i', r.number('i')), joints=r.array('i', r.number()),
                     clothes=r.array('i', r.number()))
        value['materials'] = [dict(index=r.number('H'), description=r.number('i'))
                              for _ in range(r.number())]
        value['states'] = r.number()
        value['stateNames'] = [dict(name=name(), index=r.number()) for _ in range(r.number())]
        value['flags'] = r.number('B')
    elif kind == 'NiPhysXShapeDesc':
        value = dict(shapeType=r.number(), localPose=r.array('f', 12), flags=r.number(),
                     collisionGroup=r.number('H'), material=r.number('H'), density=r.number('f'),
                     mass=r.number('f'), skinWidth=r.number('f'), name=name(),
                     nonInteractingCompartments=r.number(), collisionBits=r.array('I', 4))
        if value['shapeType'] in (5, 6):
            value['mesh'] = r.number('i')
        elif value['shapeType'] == 2:
            value['boxHalfExtents'] = vector()
        elif value['shapeType'] == 1:
            value['sphereRadius'] = r.number('f')
        else:
            raise ValueError(f'Unsupported diagnostic shape {value["shapeType"]}')
    elif kind == 'NiPhysXMeshDesc':
        mesh_name = name()
        cooked = bytes(r.take(r.number()))
        value = dict(name=mesh_name, cookedBytes=len(cooked),
                     cookedSha256=hashlib.sha256(cooked).hexdigest(),
                     cookedPrefix=cooked[:16].hex(), meshFlags=r.number(),
                     pagingMode=r.number(), flags=r.number('B'))
        # Retain the envelope and fingerprint. NXS collision geometry is still
        # opaque here; the rendering mesh is not a substitute collision hull.
    elif kind == 'NiPhysXMaterialDesc':
        value = dict(index=r.number('H'))
        value['states'] = [dict(dynamicFriction=r.number('f'), staticFriction=r.number('f'),
                                restitution=r.number('f'), dynamicFrictionV=r.number('f'),
                                staticFrictionV=r.number('f'), anisotropyDirection=vector(),
                                flags=r.number(), frictionCombine=r.number(),
                                restitutionCombine=r.number()) for _ in range(r.number())]
    elif kind == 'NiPhysXActorDesc':
        value = dict(name=name(), poses=[r.array('f', 12) for _ in range(r.number())],
                     body=r.number('i'), density=r.number('f'), flags=r.number(),
                     group=r.number('H'), dominanceGroup=r.number('H'),
                     contactReportFlags=r.number(), forceFieldMaterial=r.number('H'))
        value.update(shapes=r.array('i', r.number()), parent=r.number('i'),
                     source=r.number('i'), destination=r.number('i'))
    elif kind == 'NiPhysXBodyDesc':
        value = dict(localPose=r.array('f', 12), inertia=vector(), mass=r.number('f'))
        value['velocities'] = [dict(linear=vector(), angular=vector(), sleep=r.number('B'))
                               for _ in range(r.number())]
        for field in ['wakeUpCounter', 'linearDamping', 'angularDamping',
                      'maxAngularVelocity', 'ccdMotionThreshold']:
            value[field] = r.number('f')
        value.update(flags=r.number(), sleepLinearVelocity=r.number('f'),
                     sleepAngularVelocity=r.number('f'), solverIterations=r.number(),
                     sleepEnergyThreshold=r.number('f'), sleepDamping=r.number('f'),
                     contactReportThreshold=r.number('f'))
    elif kind in ('NiPhysXTransformDest', 'NiPhysXDynamicSrc'):
        value = dict(active=r.number('B'), interpolate=r.number('B'), node=r.number('i'))
    elif kind == 'NiPhysXD6JointDesc':
        value = dict(jointType=r.number(), name=name())
        value['actors'] = [dict(actor=r.number('i'), normal=vector(), axis=vector(),
                                anchor=vector()) for _ in range(2)]
        value.update(maxForce=r.number('f'), maxTorque=r.number('f'),
                     solverExtrapolation=r.number('f'), accelerationSpring=r.number(),
                     flags=r.number(), limitPoint=vector())
        value['limitPlanes'] = [r.array('f', 5) for _ in range(r.number())]
        value['motion'] = dict(zip(['x', 'y', 'z', 'swing1', 'swing2', 'twist'], r.array('I', 6)))
        value['limits'] = {field: dict(zip(['value', 'restitution', 'spring', 'damping'],
                                          r.array('f', 4)))
                           for field in ['linear', 'swing1', 'swing2', 'twistLow', 'twistHigh']}
        value['drives'] = {field: dict(type=r.number(), parameters=r.array('f', 3))
                           for field in ['x', 'y', 'z', 'swing', 'twist', 'slerp']}
        value.update(drivePosition=vector(), driveOrientation=r.array('f', 4),
                     driveLinearVelocity=vector(), driveAngularVelocity=vector(),
                     projectionMode=r.number(), projectionDistance=r.number('f'),
                     projectionAngle=r.number('f'), gearRatio=r.number('f'), d6Flags=r.number())
    else:
        return None
    r.finish()
    return value


def inspect(path):
    strings, blocks, _ = read_document(path)
    nodes = {index: av_object(Reader(data), strings)['name']
             for index, (kind, data) in enumerate(blocks) if kind in ('NiNode', 'NiMesh')}
    decoded = []
    for index, (kind, data) in enumerate(blocks):
        value = decode(kind, data, strings)
        if value is not None:
            if 'node' in value:
                if value['node'] not in nodes:
                    raise ValueError(f'Physics block {index} references unknown node {value["node"]}')
                value['nodeName'] = nodes[value['node']]
            decoded.append(dict(id=index, type=kind, **value))
    by_id = {block['id']: block for block in decoded}
    def require(reference, expected):
        target = by_id.get(reference)
        if target is None or target['type'] != expected:
            raise ValueError(f'Physics reference {reference} must be {expected}')
        return target
    for block in decoded:
        if block['type'] == 'NiPhysXProp':
            require(block['description'], 'NiPhysXPropDesc')
        if block['type'] == 'NiPhysXPropDesc':
            for reference in block['actors']:
                require(reference, 'NiPhysXActorDesc')
            for reference in block['joints']:
                require(reference, 'NiPhysXD6JointDesc')
            for material in block['materials']:
                target = require(material['description'], 'NiPhysXMaterialDesc')
                if target['index'] != material['index']:
                    raise ValueError('Physics material index differs from its prop reference')
        if block['type'] == 'NiPhysXShapeDesc' and 'mesh' in block:
            require(block['mesh'], 'NiPhysXMeshDesc')
        if block['type'] == 'NiPhysXD6JointDesc':
            for actor in block['actors']:
                target = by_id.get(actor['actor'])
                if target is None or target['type'] != 'NiPhysXActorDesc':
                    raise ValueError(f'Joint {block["name"]} references unknown actor')
                actor['name'] = target['name']
    return dict(file=str(path), sha256=hashlib.sha256(Path(path).read_bytes()).hexdigest(),
                blockCounts=dict(Counter(kind for kind, _ in blocks if kind.startswith('NiPhysX'))),
                textures=[s for s in strings if s.lower().endswith('.dds')], blocks=decoded)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('inputs', type=Path, nargs='+')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    reports = [inspect(path) for path in args.inputs]
    args.output.write_text(json.dumps(reports, indent=2), encoding='utf-8')
    print(json.dumps([dict(file=r['file'], decoded=len(r['blocks']), counts=r['blockCounts'])
                      for r in reports], indent=2))
