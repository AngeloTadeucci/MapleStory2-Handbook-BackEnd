"""Prepare an offline PhysX scene from inspected NIF descriptors, without recooking.

Generated C++ contains numeric descriptors and escaped names only. SDK headers,
generated data and binaries stay outside version control. Scene timing/gravity
are caller inputs, not claims about the client's final hair scene settings.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path

from inspect_hair_physics import inspect
from inspect_nif import read_document
from scan_nif import Reader


def number(value):
    if not math.isfinite(value):
        raise ValueError('Nonfinite physics descriptor')
    return format(value, '.9g') + ('.0f' if float(value).is_integer() and abs(value) < 1e9 else 'f')


def vec(values):
    return 'NxVec3(' + ','.join(number(v) for v in values) + ')'


def pose(values):
    return 'pose({' + ','.join(number(v) for v in values) + '})'


def prepare(source, output):
    report = inspect(source)
    blocks = {b['id']: b for b in report['blocks']}
    props = [b for b in blocks.values() if b['type'] == 'NiPhysXProp']
    if len(props) != 1 or props[0]['physXToWorldScale'] != 100:
        raise ValueError('Expected one source prop with scale 100')
    desc = blocks[props[0]['description']]
    if desc['clothes'] or desc['states'] != 1:
        raise ValueError('Only single-state rigid hair is supported')
    actors = [blocks[i] for i in desc['actors']]
    indices = {a['id']: i for i, a in enumerate(actors)}
    if len(actors) != 3 or [a['name'] for a in actors] != ['Bone01', 'Bone02', 'Bone03']:
        raise ValueError('This local motion diagnostic requires the Sassy three-bone chain')
    output.mkdir(parents=True, exist_ok=True)
    _, raw, _ = read_document(source)
    lines = ['// Generated from source NIF, do not hand-edit.',
             'static std::vector<NxActor*> createHair(NxPhysicsSDK& sdk, NxScene& scene, const NxMat34& placement, const std::filesystem::path& root, std::vector<NxConvexMesh*>& meshes) {',
             'std::vector<NxActor*> actors;']
    materials = {}
    for reference in desc['materials']:
        m = blocks[reference['description']]['states']
        if len(m) != 1:
            raise ValueError('Material states not supported')
        m = m[0]
        name = f'material{reference["index"]}'
        materials[reference['index']] = name
        lines += ['{ NxMaterialDesc d;']
        for key in ['dynamicFriction', 'staticFriction', 'restitution', 'dynamicFrictionV', 'staticFrictionV']:
            lines += [f'd.{key} = {number(m[key])};']
        lines += [f'd.dirOfAnisotropy = {vec(m["anisotropyDirection"])};', f'd.flags = {m["flags"]};',
                  f'd.frictionCombineMode = NxCombineMode({m["frictionCombine"]});',
                  f'd.restitutionCombineMode = NxCombineMode({m["restitutionCombine"]});',
                  f'if (!d.isValid()) fail("Invalid material");',
                  f'auto* m = scene.createMaterial(d); if (!m) fail("Material creation failed"); materialIndices.push_back(m->getMaterialIndex()); }}']
    for i, a in enumerate(actors):
        body = blocks[a['body']]
        if len(a['poses']) != 1 or len(body['velocities']) != 1:
            raise ValueError('Actor states not supported')
        if bool(body['flags'] & 128) != (i == 0):
            raise ValueError('Only Bone01 may be kinematic')
        lines += ['{ NxBodyDesc b; NxActorDesc a;', f'b.massLocalPose = {pose(body["localPose"])};',
                  f'b.massSpaceInertia = {vec(body["inertia"])};']
        for source_key, field in {'mass':'mass', 'wakeUpCounter':'wakeUpCounter', 'linearDamping':'linearDamping',
                'angularDamping':'angularDamping', 'maxAngularVelocity':'maxAngularVelocity',
                'ccdMotionThreshold':'CCDMotionThreshold', 'sleepLinearVelocity':'sleepLinearVelocity',
                'sleepAngularVelocity':'sleepAngularVelocity', 'sleepEnergyThreshold':'sleepEnergyThreshold',
                'sleepDamping':'sleepDamping', 'contactReportThreshold':'contactReportThreshold'}.items():
            lines += [f'b.{field} = {number(body[source_key])};']
        lines += [f'b.flags = {body["flags"]};', f'b.solverIterationCount = {body["solverIterations"]};',
                  f'b.linearVelocity = {vec(body["velocities"][0]["linear"])};',
                  f'b.angularVelocity = {vec(body["velocities"][0]["angular"])};',
                  f'a.globalPose = placement * {pose(a["poses"][0])};', 'a.body = &b;',
                  '// SDK mass mode: authored body mass takes priority; density must be zero.',
                  f'a.density = {number(0 if body["mass"] > 0 else a["density"])};',
                  f'a.flags = {a["flags"]}; a.group = {a["group"]}; a.dominanceGroup = {a["dominanceGroup"]};',
                  f'a.contactReportFlags = {a["contactReportFlags"]}; a.forceFieldMaterial = {a["forceFieldMaterial"]};',
                  f'a.name = {json.dumps(a["name"])};']
        for j, ref in enumerate(a['shapes']):
            shape = blocks[ref]
            if shape['shapeType'] != 5 or shape['material'] not in materials:
                raise ValueError('Only authored convex shapes with a declared material are supported')
            mesh = blocks[shape['mesh']]
            r = Reader(raw[shape['mesh']][1]); r.number()
            cooked = bytes(r.take(r.number()))
            assert hashlib.sha256(cooked).hexdigest() == mesh['cookedSha256']
            (output / f'hull-{shape["mesh"]}.nxs').write_bytes(cooked)
            s = f's{j}'
            lines += [f'ReadStream stream{j}(root / "hull-{shape["mesh"]}.nxs");',
                      f'auto* mesh{j} = sdk.createConvexMesh(stream{j});',
                      f'if (!mesh{j} || stream{j}.consumed() != stream{j}.size()) fail("Invalid cooked hull");',
                      f'meshes.push_back(mesh{j}); NxConvexShapeDesc {s}; {s}.meshData = mesh{j};',
                      f'{s}.localPose = {pose(shape["localPose"])};',
                      f'{s}.shapeFlags = {shape["flags"]}; {s}.group = {shape["collisionGroup"]};',
                      f'{s}.materialIndex = materialIndices[{list(materials).index(shape["material"])}];']
            for field in ['density', 'mass', 'skinWidth']:
                lines += [f'{s}.{field} = {number(shape[field])};']
            lines += [f'{s}.nonInteractingCompartmentTypes = {shape["nonInteractingCompartments"]};']
            for k, v in enumerate(shape['collisionBits']):
                lines += [f'{s}.groupsMask.bits{k} = {v};']
            lines += [f'if (!{s}.isValid()) fail("Invalid shape"); a.shapes.pushBack(&{s});']
        lines += ['if (!a.isValid()) fail("Invalid actor descriptor");',
                  'auto* actor = scene.createActor(a); if (!actor) fail("Actor creation failed"); actors.push_back(actor); }']
    for ref in desc['joints']:
        j = blocks[ref]
        if j['limitPlanes'] or j['jointType'] != 9:
            raise ValueError('Only D6 joints without limit planes are supported')
        lines += ['{ NxD6JointDesc d;']
        for i, a in enumerate(j['actors']):
            lines += [f'd.actor[{i}] = actors[{indices[a["actor"]]}];']
            for field, key in [('localNormal','normal'),('localAxis','axis'),('localAnchor','anchor')]:
                lines += [f'd.{field}[{i}] = {vec(a[key])};']
        for field, key in [('maxForce','maxForce'),('maxTorque','maxTorque'),('solverExtrapolationFactor','solverExtrapolation')]:
            lines += [f'd.{field} = {number(j[key])};']
        lines += [f'd.useAccelerationSpring = {j["accelerationSpring"]}; d.jointFlags = {j["flags"]};',
                  f'd.name = {json.dumps(j["name"])};']
        for field, value in j['motion'].items():
            lines += [f'd.{field}Motion = NxD6JointMotion({value});']
        for key, field in [('linear','linearLimit'),('swing1','swing1Limit'),('swing2','swing2Limit'),('twistLow','twistLimit.low'),('twistHigh','twistLimit.high')]:
            for part, value in j['limits'][key].items():
                lines += [f'd.{field}.{part} = {number(value)};']
        for key, value in j['drives'].items():
            lines += [f'd.{key}Drive.driveType = {value["type"]};']
            for field, value in zip(['spring','damping','forceLimit'], value['parameters']):
                lines += [f'd.{key}Drive.{field} = {number(value)};']
        for field in ['drivePosition','driveLinearVelocity','driveAngularVelocity']:
            lines += [f'd.{field} = {vec(j[field])};']
        w, x, y, z = j['driveOrientation']
        lines += [f'd.driveOrientation = NxQuat({vec([x,y,z])}, {number(w)});',
                  f'd.projectionMode = NxJointProjectionMode({j["projectionMode"]});',
                  f'd.projectionDistance = {number(j["projectionDistance"])};',
                  f'd.projectionAngle = {number(j["projectionAngle"])}; d.gearRatio = {number(j["gearRatio"])};',
                  f'd.flags = {j["d6Flags"]};',
                  'if (!d.isValid()) fail("Invalid D6 descriptor"); if (!scene.createJoint(d)) fail("Joint creation failed"); }']
    lines += ['return actors; }']
    lines.insert(2, 'std::vector<NxMaterialIndex> materialIndices;')
    (output / 'hair_scene.inc').write_text('\n'.join(lines)+'\n', encoding='utf-8')
    report['motionAdapter'] = {'massMode':'authored body mass; actor density zero per SDK validity requirements',
                              'jointOverrides':'none; caller must verify XML has no matching jointangle records',
                              'sceneSettings':'provided by caller; not established client final settings'}
    (output / 'source.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    return report


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('source', type=Path)
    p.add_argument('output', type=Path)
    args = p.parse_args()
    prepare(args.source, args.output)
