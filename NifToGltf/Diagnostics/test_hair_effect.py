import tempfile
import unittest
from pathlib import Path
from export_hair_effect import export_effect

ROOT = Path(__file__).resolve().parents[2] / 'Maple2Storage/Resources/SimulatorEffects'


class HairEffectTests(unittest.TestCase):
    def test_source_emitters_and_material_curves(self):
        data = export_effect(ROOT/'Models/item/hair/eff_hair_twinkle_a.nif', ROOT/'Definitions/effect/item/hair/eff_hair_twinkle_a.xml', ROOT/'Definitions/itemdata/102.xml')
        self.assertEqual(data['itemIds'], [10200121, 10200122, 10200123, 10200124])
        self.assertEqual([s['rate'] for s in data['systems']], [5, 3])
        self.assertEqual([s['capacity'] for s in data['systems']], [5, 3])
        self.assertAlmostEqual(data['systems'][0]['growTime'], 7/30)
        self.assertAlmostEqual(data['systems'][0]['lifespan'], .6)
        self.assertEqual(len(data['glow']['textures']), 3)
        self.assertAlmostEqual(data['glow']['alphaKeys'][1][1], .4)
        for s in data['systems']:
            self.assertEqual(len(s['surface']['indices']) % 3, 0)
            self.assertLess(max(s['surface']['indices']), len(s['surface']['positions']))

    def test_unknown_source_revision_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            changed = Path(directory)/'changed.nif'
            changed.write_bytes(b'unreviewed')
            with self.assertRaisesRegex(ValueError, 'Unreviewed'):
                export_effect(changed, Path('unused'), Path('unused'))


if __name__ == '__main__':
    unittest.main()
