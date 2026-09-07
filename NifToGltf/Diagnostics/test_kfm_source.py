import hashlib
from pathlib import Path
import struct
import tempfile
import unittest
from kfm_source import animation_inputs, beside, read_kfm
from wardrobe_inventory import ArchiveIndex


def fixture(model='.\\tail.nif', clip='.\\tail.kf'):
    def string(value):
        data = value.encode('ascii')
        return struct.pack('<I', len(data)) + data
    return (b';Gamebryo KFM File Version 30.2.0.3b\n\x01' + string(model) + string('Point01')
            + struct.pack('<iiffI', 0, 0, 0, 0, 1) + struct.pack('<i', 1)
            + string(clip) + string('TailIdle') + struct.pack('<Ii', 0, 0))


class KfmSourceTests(unittest.TestCase):
    def test_explicit_model_reference_preserves_the_second_tail_identity(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'tail.nif').touch()
            (root / 'tail.kf').touch()
            (root / 'tail2.kfm').write_bytes(fixture())
            index = ArchiveIndex(['tail.nif', 'tail.kf', 'tail2.kfm'])
            self.assertEqual(index.resolve('urn:tail2'), (None, []))
            source, matches, kfm = index.resolve_asset('urn:TAIL2', root)
            self.assertEqual(source, 'tail.nif')
            self.assertEqual(kfm['path'], 'tail2.kfm')
            self.assertEqual(kfm['sha256'], hashlib.sha256(fixture()).hexdigest())
            self.assertEqual(animation_inputs(root / source, root, kfm),
                             (root / 'tail2.kfm', [root / 'tail.kf']))
            (root / 'tail2.kfm').write_bytes(fixture() + b'x')
            with self.assertRaisesRegex(ValueError, 'changed'):
                animation_inputs(root / source, root, kfm)

    def test_does_not_replace_explicit_or_ambiguous_nifs(self):
        index = ArchiveIndex(['tail2.nif', 'tail2.kfm'])
        self.assertEqual(index.resolve_asset('urn:tail2', Path('unused')), ('tail2.nif', ['tail2.nif'], None))
        index = ArchiveIndex(['a/tail2.nif', 'b/tail2.nif', 'tail2.kfm'])
        self.assertEqual(index.resolve_asset('urn:tail2', Path('unused'))[1], ['a/tail2.nif', 'b/tail2.nif'])
        index = ArchiveIndex(['a/tail2.kfm', 'b/tail2.kfm'])
        self.assertEqual(index.resolve_asset('urn:tail2', Path('unused')), (None, ['a/tail2.kfm', 'b/tail2.kfm'], None))

    def test_missing_references_and_escaping_paths_fail_closed(self):
        for reference in ['../tail.nif', '/tail.nif', 'D:\\tail.nif', '\\tail.nif', '']:
            with self.subTest(reference=reference), self.assertRaises(ValueError):
                beside('a/tail2.kfm', reference)
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'tail2.kfm').write_bytes(fixture())
            with self.assertRaisesRegex(ValueError, 'missing model'):
                ArchiveIndex(['tail2.kfm']).resolve_asset('urn:tail2', root)
            (root / 'tail.nif').touch()
            _, _, kfm = ArchiveIndex(['tail2.kfm', 'tail.nif']).resolve_asset('urn:tail2', root)
            with self.assertRaisesRegex(ValueError, 'Missing or ambiguous'):
                animation_inputs(root / 'tail.nif', root, kfm)

    def test_parser_consumes_payload_and_preserves_sequence(self):
        result = read_kfm(fixture())
        self.assertEqual(result['master'], 'Point01')
        self.assertEqual(result['clips'], [dict(event=1, file='.\\tail.kf', name='TailIdle')])
        for data in [fixture()[:-1], fixture() + b'x', b'wrong header' + fixture()[12:]]:
            with self.assertRaises(ValueError):
                read_kfm(data)

    def test_real_sassy_kfms_resolve_one_model_and_clip(self):
        root = Path(__file__).resolve().parents[1] / 'obj/hair-investigation/source'
        paths = [p.relative_to(root).as_posix() for p in root.rglob('*') if p.is_file()]
        if not any(p.lower().endswith('00200010_f_pipi_p2_a.kfm') for p in paths):
            self.skipTest('Local Sassy KFM extraction is not present')
        index = ArchiveIndex(paths)
        source, _, kfm = index.resolve_asset('urn:00200010_F_PiPi_P2_A', root)
        self.assertTrue(source.lower().endswith('00200010_f_pipi_p_a.nif'))
        self.assertEqual(kfm['sha256'], 'a3b0db2c52b63e08e138023eddd8e969d284cd3d5110cf707b99dfccd14a62c6')
        _, clips = animation_inputs(root / source, root, kfm)
        self.assertEqual(len(clips), 1)
        self.assertTrue(clips[0].name.lower().endswith('00200010_f_pipi_p_a.kf'))


if __name__ == '__main__':
    unittest.main()
