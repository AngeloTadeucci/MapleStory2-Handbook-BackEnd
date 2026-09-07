"""Optional integration check using the user's existing client PhysX runtime."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

from inspect_nif import read_document
from scan_nif import Reader

ROOT = Path(__file__).resolve().parents[1] / 'obj/hair-investigation'
PROBE = ROOT / 'probe_physx284.exe'
SOURCE = ROOT / 'source/0/02'
DLLS = Path(os.environ.get('MS2_PHYSX_DLL_DIR', 'D:/MS2/KMS2 Debug/x64'))


@unittest.skipUnless(PROBE.exists() and SOURCE.exists() and (DLLS / 'PhysXCore64.dll').exists(),
                     'Build the optional native probe and install local source evidence first')
class PhysX284ProbeTests(unittest.TestCase):
    def test_client_runtime_reads_both_curled_tail_hulls_and_matches_joint_layout(self):
        with tempfile.TemporaryDirectory(prefix='probe-test-', dir=ROOT) as directory:
            paths = []
            for suffix in ['p', 'p2']:
                _, blocks, _ = read_document(SOURCE / f'10200031_f_ironrollhair_{suffix}.nif')
                for index, (kind, data) in enumerate(blocks):
                    if kind != 'NiPhysXMeshDesc':
                        continue
                    reader = Reader(data)
                    reader.number()  # String-table index, outside the cooked blob.
                    path = Path(directory) / f'{suffix}-{index}.nxs'
                    path.write_bytes(bytes(reader.take(reader.number())))
                    paths.append(path)
            result = subprocess.run([PROBE, DLLS, *paths], capture_output=True, text=True, timeout=30)
            self.assertEqual(result.returncode, 0, result.stderr)
            report = json.loads(result.stdout)
            self.assertEqual(report['version'], 284)
            self.assertEqual(report['apiRevision'], 1)
            self.assertTrue(report['clientCoreVerified'])
            self.assertEqual(report['d6Offsets'], dict(localNormal0=0x20, localAxis0=0x38,
                linearLimit=0xa8, swing1Limit=0xb8, swing2Limit=0xc8,
                twistLow=0xd8, twistHigh=0xe8, xDrive=0xf8))
            self.assertEqual([m['vertices'] for m in report['meshes']], [17, 19, 21, 21] * 2)
            self.assertEqual([m['triangles'] for m in report['meshes']], [30, 34, 38, 38] * 2)
            self.assertTrue(all(m['complete'] and m['loaded'] for m in report['meshes']))
            # A malformed caller input must fail in the bounded reader.
            paths[0].write_bytes(paths[0].read_bytes()[:8])
            bad = subprocess.run([PROBE, DLLS, paths[0]], capture_output=True, text=True, timeout=30)
            self.assertNotEqual(bad.returncode, 0)
            self.assertIn('Invalid cooked stream operation', bad.stderr)


if __name__ == '__main__':
    unittest.main()
