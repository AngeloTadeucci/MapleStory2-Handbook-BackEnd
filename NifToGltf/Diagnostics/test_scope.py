from pathlib import Path
import tempfile
import unittest
from create_scope_plan import create_plan
from inspect_nif import read_document
from test_scan_nif import nif


class ScopeTests(unittest.TestCase):
    def test_effect_directory_is_not_an_exclusion(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'Effect').mkdir()
            (root / 'Effect/instrument.nif').write_bytes(nif())
            (root / 'Effect/reveal.nif').write_bytes(nif())
            plan = create_plan(root, {'version': 1, 'effects': [
                {'input': 'Effect/reveal.nif', 'reason': 'Explicit source effect entry'}]})
            self.assertEqual(plan['models'][0], {'input': 'Effect/instrument.nif'})
            self.assertEqual(plan['models'][1]['excludeEffect'], 'Explicit source effect entry')

    def test_stale_exclusion_cannot_silently_disappear(self):
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaisesRegex(ValueError, 'missing'):
                create_plan(Path(directory), {'version': 1, 'effects': [
                    {'input': 'missing.nif', 'reason': 'Effect'}]})

    def test_inspector_preserves_masked_types_and_rejects_truncation(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'sample.nif'
            path.write_bytes(nif(metadata=b'abc', type_index=0x8000))
            strings, blocks, roots = read_document(path)
            self.assertEqual(strings, [])
            self.assertEqual(roots, [0])
            self.assertEqual(blocks[0][0], 'NiDataStream\x011\x0118')
            path.write_bytes(path.read_bytes()[:-1])
            with self.assertRaises(ValueError):
                read_document(path)
