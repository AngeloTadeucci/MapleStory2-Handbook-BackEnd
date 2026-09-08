"""Source-to-actor coordinate and actual native motion regressions."""
import copy
import json
from pathlib import Path
import subprocess
import tempfile
import unittest

from bake_hair_motion import multiply, inverse, prepare_input

ROOT=Path(__file__).resolve().parents[1]/'obj/hair-investigation/sassy/motion'
DLLS=Path('D:/MS2/KMS2 Debug/x64')


def relative_rotation(tail):
    x,y,z,w=tail[0][3:];x,y,z=-x,-y,-z
    X,Y,Z,W=tail[2][3:]
    return [w*X+x*W+y*Z-z*Y,w*Y-x*Z+y*W+z*X,w*Z+x*Y-y*X+z*W,w*W-x*X-y*Y-z*Z]


class RigidTransformTests(unittest.TestCase):
    def test_rotation_translation_roundtrip_and_composition(self):
        a=[0,-1,0,1,0,0,0,0,1,3,4,5]
        identity=[1,0,0,0,1,0,0,0,1,0,0,0]
        self.assertEqual(multiply(a,inverse(a)),identity)
        b=identity[:];b[9:]=[2,0,0]
        self.assertEqual(multiply(a,b)[9:],[3,6,5])


@unittest.skipUnless((ROOT/'capture.json').exists() and (ROOT/'simulate_hair284.exe').exists()
                     and (DLLS/'PhysXCore64.dll').exists(),'Local motion inputs/runtime required')
class NativeHairMotionTests(unittest.TestCase):
    def test_rest_alignment_and_bad_capture_rejection(self):
        capture=json.loads((ROOT/'capture.json').read_text(encoding='utf-8'))
        source=json.loads((ROOT/'source.json').read_text(encoding='utf-8'))
        _,errors=prepare_input(capture,source)
        self.assertLess(max(errors),1e-6)
        bad=copy.deepcopy(capture);bad['rest'][0][2][9]+=0.01
        with self.assertRaisesRegex(ValueError,'rest transforms differ'):prepare_input(bad,source)
        bad=copy.deepcopy(capture);bad['scale']=1.2
        with self.assertRaisesRegex(ValueError,'size'):prepare_input(bad,source)

    def test_solver_tracks_root_and_moves_both_destination_chains(self):
        result=subprocess.run([str(ROOT/'simulate_hair284.exe'),str(DLLS),str(ROOT),str(ROOT/'playback-input.bin')],
                              capture_output=True,text=True,timeout=30)
        self.assertEqual(result.returncode,0,result.stderr)
        self.assertEqual(result.stderr,'')
        motion=json.loads(result.stdout)['frames']
        capture=json.loads((ROOT/'capture.json').read_text(encoding='utf-8'))
        self.assertEqual(len(motion),481)
        for t in range(2):
            for frame,target in zip(motion,capture['frames']):
                self.assertLess(max(abs(a-b) for a,b in zip(frame[t][0][:3],target[t][9:])),1e-5)
            initial=relative_rotation(motion[0][t])
            self.assertTrue(any(abs(v-initial[i])>0.01 for frame in motion
                                for i,v in enumerate(relative_rotation(frame[t]))))
        with tempfile.TemporaryDirectory(dir=ROOT) as tmp:
            bad=Path(tmp)/'truncated.bin';bad.write_bytes((ROOT/'playback-input.bin').read_bytes()[:-4])
            result=subprocess.run([str(ROOT/'simulate_hair284.exe'),str(DLLS),str(ROOT),str(bad)],
                                  capture_output=True,text=True,timeout=30)
            self.assertNotEqual(result.returncode,0)
            self.assertIn('Truncated motion input',result.stderr)
            data=bytearray((ROOT/'playback-input.bin').read_bytes())
            # Replace the first placement with a determinant-one shear.
            import struct
            struct.pack_into('<12f',data,32,1,1,0,0,1,0,0,0,1,0,0,0)
            bad.write_bytes(data)
            result=subprocess.run([str(ROOT/'simulate_hair284.exe'),str(DLLS),str(ROOT),str(bad)],
                                  capture_output=True,text=True,timeout=30)
            self.assertNotEqual(result.returncode,0)
            self.assertIn('orthonormal',result.stderr)


if __name__=='__main__':unittest.main()
