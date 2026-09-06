import tempfile
import unittest
import hashlib
import json
from pathlib import Path
from simulator_library import read_items
from package_simulator import apply_review


class SimulatorLibraryTest(unittest.TestCase):
    def test_review_requires_the_inspected_bytes_and_complete_available_item(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'body.gltf').write_text('body')
            (root / 'gear.gltf').write_text('gear')
            manifest = {'assets': [{'id': 'body', 'uri': 'body.gltf'},
                                   {'id': 'gear', 'uri': 'gear.gltf', 'skeleton': 'body.nif'}]}
            original = {'items': [{'itemId': 1, 'bodyVariant': 'male', 'parts': [{'assetId': 'gear'}],
                                   'availability': 'preview'}]}
            review = {'catalogHash': hashlib.sha256(json.dumps(original, sort_keys=True).encode()).hexdigest(),
                      'assetHashes': {a['id']: hashlib.sha256((root / a['uri']).read_bytes()).hexdigest() for a in manifest['assets']},
                      'fileHashes': {}, 'items': [{'itemId': 1, 'bodyVariant': 'male', 'evidence': ['matrix.jpg']}]}
            approved = json.loads(json.dumps(original))
            apply_review(root, manifest, approved, review)
            self.assertEqual(approved['items'][0]['availability'], 'verified')
            (root / 'gear.gltf').write_text('changed geometry')
            with self.assertRaisesRegex(ValueError, 'asset changed'):
                apply_review(root, manifest, original, review)
            (root / 'gear.gltf').write_text('gear')
            del review['assetHashes']['gear']
            with self.assertRaisesRegex(ValueError, 'omits an item part'):
                apply_review(root, manifest, original, review)
            original['items'][0]['availability'] = 'unavailable'
            review['catalogHash'] = hashlib.sha256(json.dumps(original, sort_keys=True).encode()).hexdigest()
            with self.assertRaisesRegex(ValueError, 'unavailable item'):
                apply_review(root, manifest, original, review)

    def test_identity_gender_bundles_and_urn_ambiguity(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory) / 'Sources/Xml'
            (root / 'itemmodel').mkdir(parents=True)
            (root / 'itemmodel/122.xml').write_text('''<ms2><ItemModel id="12200001"><slots>
            <slot name="CL"><asset name="Data/Resource/Model/Item/1/22/model_m.nif" gender="0"/><asset name="urn:model_f" gender="1"/></slot>
            <slot name="PA"><asset name="Data/Resource/Model/Item/1/22/model_m.nif" gender="0"/></slot>
            </slots><cutting><mesh name="SH_Skin" gender="0"/></cutting></ItemModel></ms2>''')
            items = list(read_items(root, ['1/22/model_m.nif', '1/22/model_f.nif']))
            self.assertEqual(items[0]['itemId'], 12200001)
            self.assertEqual(items[0]['slots'], ['CL', 'PA'])
            self.assertEqual(items[0]['cutting'], ['SH_Skin'])
            self.assertEqual(items[1]['parts'][0]['source'], '1/22/model_f.nif')
            self.assertEqual(items[1]['cutting'], [])
            ambiguous = list(read_items(root, ['1/22/model_m.nif', '1/22/model_f.nif', 'other/model_f.nif']))
            self.assertIsNone(ambiguous[1]['parts'][0]['source'])


if __name__ == '__main__':
    unittest.main()
