import tempfile
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET
from wardrobe_inventory import ArchiveIndex, reconcile, select_environment, summary


class WardrobeInventoryTests(unittest.TestCase):
    def test_archive_resolution_keeps_paths_and_ambiguous_urns(self):
        index = ArchiveIndex(['Item/a/shared.nif', 'Item/b/shared.nif', 'Effect/hat.nif'])
        self.assertEqual(index.resolve('urn:shared'), (None, ['Item/a/shared.nif', 'Item/b/shared.nif']))
        self.assertEqual(index.resolve('Data\\Resource\\Model\\Effect\\HAT.nif')[0], 'Effect/hat.nif')
        self.assertEqual(index.resolve('Data/Resource/Model/Item/a/shared.nif')[0], 'Item/a/shared.nif')

    def test_feature_selection_retains_live_override_and_rejects_future(self):
        item = ET.fromstring('<item><environment/><environment feature="future"/><environment feature="live"/></item>')
        self.assertEqual(select_environment(item, {'future': 99, 'live': 20}, 80).get('feature'), 'live')
        self.assertIsNone(select_environment(ET.fromstring('<item><environment feature="unknown"/></item>'), {}, 80))

    def test_shared_preset_missing_names_and_nonvisual_slots_do_not_drop_ids(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            files = {
                'table/feature.xml': '<ms2/>',
                'table/feature_setting.xml': '<ms2><setting type="Live" KR="80"/></ms2>',
                'string/kr/itemname.xml': '<ms2><key id="1" name="First"/></ms2>',
                'itemmodel/1.xml': '''<ms2>
                  <ItemModel id="10"><slots><slot name="EY"><asset name="urn:glasses" gender="1"/></slot></slots></ItemModel>
                  <ItemModel id="20"><slots><slot name="RI"><asset name="Data/Resource/Model/Item/Empty.nif"/></slot></slots></ItemModel>
                </ms2>''',
                'itemdata/1.xml': '''<ms2>
                  <item id="1"><environment><limit genderLimit="1"/><tool itemPreset="10"/></environment></item>
                  <item id="2"><environment><limit genderLimit="1"/><tool itemPreset="10"/></environment></item>
                  <item id="3"><environment><tool itemPreset="20"/></environment></item>
                  <item id="4"><environment><property category="BD"/></environment></item>
                  <item id="5"/>
                  <item id="6"><environment><property category="CP"/><tool itemPreset="999"/></environment></item>
                </ms2>'''}
            for name, contents in files.items():
                path = root / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(contents)
            inv = reconcile(root, ['Item/glasses.nif', 'Item/empty.nif'])
            self.assertEqual([(i['itemId'], i['bodyVariant']) for i in inv['items']], [(1, 'female'), (2, 'female'), (3, 'female'), (3, 'male'), (6, 'female'), (6, 'male')])
            self.assertEqual(inv['items'][0]['family'], inv['items'][1]['family'])
            self.assertEqual(inv['items'][2]['classification'], 'nonvisual')
            self.assertIn('999', inv['items'][-1]['blockers'][0])
            report = summary(inv)
            self.assertEqual(report['scopedItems'] + len(inv['excluded']), report['sourceItems'])
            self.assertEqual(report['bodyPairs'], 6)


if __name__ == '__main__': unittest.main()
