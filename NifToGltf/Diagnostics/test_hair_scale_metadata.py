"""Check scale defaults and customization independently of XML preset count."""
from pathlib import Path
import tempfile
import unittest

from hair_scale_metadata import extract


class HairScaleMetadataTests(unittest.TestCase):
    def parse(self, xml):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / 'hair.xml'
            path.write_text(xml, encoding='utf-8')
            return extract(path)['items']

    def test_channels_bounds_and_presets_are_independent(self):
        item = self.parse('''<root><ItemModel id="1"><slots><slot name="HR">
            <scale value="0.3,0.5,0.9" reverse="1"/>
            <scale value="1" min="0.4" max="1.5"/>
            </slot></slots><customize scale="1"/></ItemModel></root>''')['1']
        self.assertTrue(item['enabled'])
        self.assertEqual(item['scales'], [
            dict(min=0, max=1, reverse=True, values=[0.3, 0.5, 0.9]),
            dict(min=0.4, max=1.5, reverse=False, values=[1])])

    def test_missing_customize_scale_does_not_enable_controls(self):
        item = self.parse('''<root><ItemModel id="1"><slots><slot name="HR">
            <scale value="0.8,1,1.2" min="0.8" max="1.2"/>
            </slot></slots><customize color="1"/></ItemModel></root>''')['1']
        self.assertFalse(item['enabled'])
        self.assertEqual(item['scales'][0]['max'], 1.2)

    def test_rejects_nonfinite_or_inverted_bounds_and_unknown_reverse(self):
        for attrs in ['min="nan"', 'min="2" max="1"', 'value="inf"', 'reverse="2"']:
            with self.subTest(attrs=attrs), self.assertRaises(ValueError):
                self.parse(f'<root><ItemModel id="1"><slots><slot><scale {attrs}/></slot></slots></ItemModel></root>')


if __name__ == '__main__':
    unittest.main()
