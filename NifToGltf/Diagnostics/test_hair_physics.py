"""Decoder tests, including the optional read-only local client evidence."""
import math
import hashlib
from pathlib import Path
import struct
import unittest

from inspect_hair_physics import decode, inspect

SOURCE = Path(__file__).resolve().parents[1] / 'obj/hair-investigation/source/0/02'


class HairPhysicsTests(unittest.TestCase):
    def test_cooked_mesh_envelope_preserves_payload_identity_and_rejects_bad_lengths(self):
        cooked = b'NXS\x01CVXM\x03\x00\x00\x00\x00\x00\x00\x00'
        payload = struct.pack('<II', 0, len(cooked)) + cooked + struct.pack('<IIB', 7, 0, 1)
        mesh = decode('NiPhysXMeshDesc', payload, ['Hull'])
        self.assertEqual(mesh, dict(name='Hull', cookedBytes=16,
                         cookedSha256=hashlib.sha256(cooked).hexdigest(),
                         cookedPrefix=cooked.hex(), meshFlags=7, pagingMode=0, flags=1))
        for bad in [payload[:-1], payload + b'\x00', payload[:4] + struct.pack('<I', 1000) + payload[8:]]:
            with self.assertRaises(ValueError):
                decode('NiPhysXMeshDesc', bad, ['Hull'])

    @unittest.skipUnless(SOURCE.exists(), 'Local source extraction is not installed')
    def test_curled_collision_hulls_materials_and_prop_references(self):
        for suffix in ['p', 'p2']:
            blocks = inspect(SOURCE / f'10200031_f_ironrollhair_{suffix}.nif')['blocks']
            hulls = [b for b in blocks if b['type'] == 'NiPhysXMeshDesc']
            self.assertEqual(len(hulls), 4)
            self.assertTrue(all(b['cookedPrefix'].startswith('4e5853014356584d') for b in hulls))
            self.assertEqual(len({b['cookedSha256'] for b in hulls}), 4)
            materials = [b for b in blocks if b['type'] == 'NiPhysXMaterialDesc']
            self.assertEqual([m['index'] for m in materials], [1, 2])
            self.assertEqual(materials[0]['states'][0]['dynamicFriction'], 1)
            self.assertAlmostEqual(materials[0]['states'][0]['restitution'], 0.8)
            self.assertAlmostEqual(materials[1]['states'][0]['dynamicFriction'], 0.2)
            self.assertEqual(materials[1]['states'][0]['restitution'], 0)
            prop = next(b for b in blocks if b['type'] == 'NiPhysXPropDesc')
            self.assertEqual(len(prop['actors']), 4)
            self.assertEqual(len(prop['joints']), 3)
            self.assertEqual([m['index'] for m in prop['materials']], [1, 2])

    def test_target_payload_rejects_truncation_and_trailing_bytes(self):
        payload = struct.pack('<BBi', 1, 0, 12)
        self.assertEqual(decode('NiPhysXTransformDest', payload, []),
                         dict(active=1, interpolate=0, node=12))
        for bad in [payload[:-1], payload + b'\x00']:
            with self.assertRaises(ValueError):
                decode('NiPhysXTransformDest', bad, [])

    @unittest.skipUnless(SOURCE.exists(), 'Local source extraction is not installed')
    def test_curled_tail_joint_chain_and_world_scale(self):
        for suffix in ['p', 'p2']:
            report = inspect(SOURCE / f'10200031_f_ironrollhair_{suffix}.nif')
            blocks = report['blocks']
            self.assertEqual(next(b['physXToWorldScale'] for b in blocks
                                  if b['type'] == 'NiPhysXProp'), 100)
            self.assertEqual([b['nodeName'] for b in blocks if b['type'] == 'NiPhysXTransformDest'],
                             ['Bone02', 'Bone03', 'Bone04'])
            joints = sorted((b for b in blocks if b['type'] == 'NiPhysXD6JointDesc'), key=lambda b: b['name'])
            self.assertEqual([b['name'] for b in joints], ['UConstraint01', 'UConstraint02', 'UConstraint03'])
            for index, (joint, angle) in enumerate(zip(joints, [15, 25, 40])):
                self.assertEqual([a['name'] for a in joint['actors']],
                                 [f'Bone0{index + 1}', f'Bone0{index + 2}'])
                self.assertEqual(joint['motion'], dict(x=0, y=0, z=0, swing1=1, swing2=1, twist=0))
                self.assertAlmostEqual(joint['limits']['swing1']['value'], math.radians(angle), places=6)
                self.assertTrue(all(d['type'] == 0 for d in joint['drives'].values()))

    @unittest.skipUnless(SOURCE.exists(), 'Local source extraction is not installed')
    def test_flower_up_contains_no_physics_blocks(self):
        report = inspect(SOURCE / '00200008_f_flowerup.nif')
        self.assertEqual(report['blockCounts'], {})
        self.assertEqual(report['blocks'], [])


if __name__ == '__main__':
    unittest.main()
