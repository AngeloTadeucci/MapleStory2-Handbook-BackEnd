import tempfile
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET
from lith_sources import build, resolve_environment
from wardrobe_inventory import reconcile


def environments(xml):
    return ET.fromstring(f'<ms2>{xml}</ms2>').findall('environment')


class ResolveEnvironmentTests(unittest.TestCase):
    # Expected choices follow Maple2.File.Parser FeatureLocaleFilter.Resolve, which the server uses.
    def test_matching_locale_beats_empty_locale(self):
        chosen = resolve_environment(environments(
            '<environment feature="" locale="" n="empty"/><environment feature="" locale="NA" n="na"/>'), {}, 'NA')
        self.assertEqual(chosen.get('n'), 'na')

    def test_other_locales_and_disabled_features_are_ineligible(self):
        chosen = resolve_environment(environments(
            '<environment locale="KR" n="kr"/><environment feature="Future" n="future"/><environment n="base"/>'),
            {'Live': 10}, 'NA')
        self.assertEqual(chosen.get('n'), 'base')
        self.assertIsNone(resolve_environment(environments('<environment locale="KR"/>'), {}, 'NA'))

    def test_highest_feature_wins_and_ties_take_the_later_name(self):
        features = {'A': 5, 'B': 5, 'Low': 1}
        chosen = resolve_environment(environments(
            '<environment n="base"/><environment feature="Low" n="low"/>'
            '<environment feature="B" n="b"/><environment feature="A" n="a"/>'), features, 'NA')
        self.assertEqual(chosen.get('n'), 'b')

    def test_last_unfeatured_entry_wins_until_a_featured_one_does(self):
        chosen = resolve_environment(environments('<environment n="first"/><environment n="second"/>'), {}, 'NA')
        self.assertEqual(chosen.get('n'), 'second')
        chosen = resolve_environment(environments(
            '<environment feature="F" n="featured"/><environment n="after"/>'), {'F': 0}, 'NA')
        self.assertEqual(chosen.get('n'), 'featured')


class BuildTests(unittest.TestCase):
    def test_per_item_xml_becomes_the_inventory_layout(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            xml, output = root / 'Xml', root / 'out'
            files = {
                'table/feature.xml': '<ms2><feature name="Late" NA="50"/></ms2>',
                'table/feature_setting.xml': '<ms2><setting type="Live" NA="47"/></ms2>',
                'string/en/itemname.xml': '<ms2><key id="11300001" name="Cap"/><key id="11300002" name="Twin"/></ms2>',
                # Two items share preset 11300001 but carry different slots, as 334 Lith presets do.
                'item/1/13/11300001.xml': '''<ms2><environment feature="" locale="">
                    <slots><slot name="CP"><asset name="Data/Resource/Model/Item/1/13/cap.nif" gender="2"/></slot></slots>
                    <limit genderLimit="2"/><property category="CP"/><cutting/>
                    <customize colorPalette="5" defaultColorIndex="-1"><HR scale="1"/><FD translation="1" rotation="0" scale="1"/>
                    <CP xrotation="1" scale="0" attach="1"><transform position="52.78, 3.0, 18.5" rotation="0.44, -0.87, 0.02"/></CP></customize>
                    <tool itemPreset="11300001"/></environment>
                    <environment feature="Late" locale=""><slots/><tool itemPreset="1"/></environment></ms2>''',
                'item/1/13/11300002.xml': '''<ms2><environment feature="" locale="">
                    <slots><slot name="CP"><asset name="Data/Resource/Model/Item/1/13/other.nif" gender="1"/>
                    <asset name="urn:gamebryo-animation:urn:gamebryo-animation:urn:wing" gender="1"/><dummy gender="2" translation="0,1,0"/></slot></slots>
                    <limit genderLimit="1"/><property category="CP"/><tool itemPreset="11300001"/></environment></ms2>''',
                'item/1/13/11300003.xml': '<ms2><environment locale="KR"><tool itemPreset="3"/></environment></ms2>',
                # Furniture carries its model in an unnamed slot. It is not equipment.
                'item/5/01/50100002.xml': '''<ms2><environment><slots><slot name=""><asset name="Data/Resource/Model/Item/lamp.nif"/></slot></slots>
                    <property category="LA"/><tool itemPreset="50100002"/></environment></ms2>'''}
            for name, contents in files.items():
                path = xml / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(contents, encoding='utf-8')
            counts = build(xml, output, 'NA', 'Live', 'en')
            self.assertEqual(counts, {'items': 4, 'resolved': 3, 'inactive': 1, 'equipment': 2})
            furniture = ET.parse(output / 'itemdata/501.xml').getroot().find('item/environment/tool')
            self.assertEqual((furniture.get('itemPreset'), furniture.get('lithItemPreset')), ('0', '50100002'))
            self.assertFalse((output / 'itemmodel/501.xml').exists())
            tool = ET.parse(output / 'itemdata/113.xml').getroot().find("item[@id='11300002']/environment/tool")
            self.assertEqual((tool.get('itemPreset'), tool.get('lithItemPreset')), ('11300002', '11300001'))
            model = ET.parse(output / 'itemmodel/113.xml').getroot().find("ItemModel[@id='11300001']")
            self.assertIsNone(model.find('slots/slot/asset').get('gender'))
            customize = model.find('customize')
            self.assertEqual(customize.get('colorPalette'), '5')
            self.assertIsNone(customize.get('defaultColorIndex'))
            # A CP-only item takes CP's flags: scale 0 is omitted, rotation comes from xrotation.
            self.assertEqual({k: customize.get(k) for k in ['scale', 'rotation', 'translation', 'capAttach']},
                             {'scale': None, 'rotation': '1', 'translation': '1', 'capAttach': '1'})
            self.assertEqual(customize.find('capTransform').attrib, {'position': '52.78, 3.0, 18.5', 'rotation': '0.44, -0.87, 0.02'})
            other = ET.parse(output / 'itemmodel/113.xml').getroot().find("ItemModel[@id='11300002']")
            self.assertEqual([a.get('name') for a in other.iter('asset')][1], 'urn:wing')
            self.assertIsNone(other.find('slots/slot/dummy').get('gender'))
            for extra in ['table/feature.xml', 'table/feature_setting.xml', 'string/en/itemname.xml']:
                (output / extra).parent.mkdir(parents=True, exist_ok=True)
                (output / extra).write_bytes((xml / extra).read_bytes())
            inventory = reconcile(output, ['1/13/cap.nif', '1/13/other.nif', '1/18/wing.nif'], language='en', locale='NA')
            pairs = {(i['itemId'], i['bodyVariant']): i for i in inventory['items']}
            self.assertEqual(sorted(pairs), [(11300001, 'female'), (11300001, 'male'), (11300002, 'female')])
            self.assertEqual(pairs[(11300001, 'male')]['parts'][0]['source'], '1/13/cap.nif')
            self.assertEqual(sorted(p['source'] for p in pairs[(11300002, 'female')]['parts']), ['1/13/other.nif', '1/18/wing.nif'])
            self.assertEqual(pairs[(11300001, 'male')]['sourceName'], 'Cap')
            self.assertNotIn('defaultColorIndex', pairs[(11300001, 'male')]['customize'])
            self.assertEqual(sorted((e['itemId'], e['classification']) for e in inventory['excluded']),
                             [(11300003, 'inactive'), (50100002, 'nonwearable')])


if __name__ == '__main__': unittest.main()
