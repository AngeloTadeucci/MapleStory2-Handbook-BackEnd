import unittest
from expand_wardrobe import declares_baseline_models, duplicate_wardrobe_assets, prefer_baseline_assets


def asset(identity, source, body='male', slot='HR', **extra):
    return {'id': identity, 'input': f'WardrobeSources/Item/0/02/{source}.nif', 'bodyVariant': body, 'slot': slot,
            'uri': f'wardrobe/{identity}.gltf', **extra}


class DuplicateWardrobeAssetsTests(unittest.TestCase):
    def test_baseline_reproduced_by_a_new_conversion_is_dropped(self):
        assets = {a['id']: a for a in [asset('wardrobe-old', 'hair_a'), asset('wardrobe-new', 'HAIR_A')]}
        self.assertEqual(duplicate_wardrobe_assets({'wardrobe-old'}, assets, {'wardrobe-new'}), ['wardrobe-old'])

    def test_referenced_baseline_is_kept_and_its_unused_rebuild_dropped(self):
        assets = {a['id']: a for a in [asset('wardrobe-old', 'hair_a'), asset('wardrobe-new', 'hair_a')]}
        self.assertEqual(duplicate_wardrobe_assets({'wardrobe-old'}, assets, {'wardrobe-old'}), ['wardrobe-new'])

    def test_unused_rebuild_of_an_unused_baseline_is_kept(self):
        assets = {a['id']: a for a in [asset('wardrobe-old', 'hair_c'), asset('wardrobe-new', 'hair_c')]}
        self.assertEqual(duplicate_wardrobe_assets({'wardrobe-old'}, assets, set()), ['wardrobe-old'])

    def test_both_copies_in_use_are_kept(self):
        assets = {a['id']: a for a in [asset('wardrobe-old', 'hair_a'), asset('wardrobe-new', 'hair_a')]}
        self.assertEqual(duplicate_wardrobe_assets({'wardrobe-old'}, assets, {'wardrobe-old', 'wardrobe-new'}), [])

    def test_different_body_placement_or_form_is_not_a_duplicate(self):
        assets = {a['id']: a for a in [
            asset('wardrobe-old-f', 'hair_a', body='female'),
            asset('wardrobe-old-lh', 'blade', slot='OH', hand='LH'),
            asset('wardrobe-old-c', 'hair_c', alternateOf='WardrobeSources/Item/0/02/hair_a.nif'),
            asset('wardrobe-new', 'hair_a'),
            asset('wardrobe-new-rh', 'blade', slot='OH', hand='RH'),
            asset('wardrobe-new-c', 'hair_c')]}
        baseline = {'wardrobe-old-f', 'wardrobe-old-lh', 'wardrobe-old-c'}
        self.assertEqual(duplicate_wardrobe_assets(baseline, assets, set()), [])

    def test_non_wardrobe_baseline_assets_are_never_touched(self):
        assets = {a['id']: a for a in [asset('f_body', 'f_body'), asset('wardrobe-new', 'f_body')]}
        self.assertEqual(duplicate_wardrobe_assets({'f_body'}, assets, set()), [])


class PreferBaselineAssetsTests(unittest.TestCase):
    def assets(self):
        return {a['id']: a for a in [asset('10300001-male-0', 'braveface', slot='FA'), asset('wardrobe-new', 'braveface', slot='FA'),
                                     asset('wardrobe-cap', 'cap', slot='CP')]}

    def test_fresh_copy_of_a_used_baseline_model_is_replaced(self):
        entries = [{'parts': [{'assetId': '10300001-male-0'}]}, {'parts': [{'assetId': 'wardrobe-new'}]}]
        self.assertEqual(prefer_baseline_assets(entries, {'10300001-male-0'}, self.assets()), 1)
        self.assertEqual(entries[1]['parts'][0]['assetId'], '10300001-male-0')

    def test_fresh_model_without_a_used_baseline_is_kept(self):
        entries = [{'parts': [{'assetId': 'wardrobe-new'}]}, {'parts': [{'assetId': 'wardrobe-cap'}]}]
        self.assertEqual(prefer_baseline_assets(entries, {'10300001-male-0'}, self.assets()), 0)
        self.assertEqual([e['parts'][0]['assetId'] for e in entries], ['wardrobe-new', 'wardrobe-cap'])

    def test_hair_forms_and_hand_parts_follow_the_baseline_too(self):
        entries = [{'parts': [{'assetId': '10300001-male-0'}]},
                   {'parts': [], 'hairForms': {'c': ['wardrobe-new']}, 'handParts': {'RH': ['wardrobe-new']}}]
        self.assertEqual(prefer_baseline_assets(entries, {'10300001-male-0'}, self.assets()), 2)
        self.assertEqual((entries[1]['hairForms']['c'], entries[1]['handParts']['RH']), (['10300001-male-0'], ['10300001-male-0']))


class DeclaresBaselineModelsTests(unittest.TestCase):
    assets = {'cap-0': {'input': 'GeloSources/Item/1/13/11300002_c_casualcap.nif'},
              'hair-0': {'input': 'SimulatorEffects/Hair/0/02/10200006_f_bubblywave_a.nif'},
              'decal': {}}

    def source(self, *paths):
        return {'parts': [{'source': path} for path in paths]}

    def test_same_model_from_another_root_reuses_the_baseline(self):
        baseline = {'parts': [{'assetId': 'cap-0'}]}
        self.assertTrue(declares_baseline_models(baseline, self.source('1/13/11300002_C_CasualCap.nif'), self.assets))

    def test_simulator_effect_roots_compare_by_archive_path(self):
        baseline = {'parts': [{'assetId': 'hair-0'}]}
        self.assertTrue(declares_baseline_models(baseline, self.source('0/02/10200006_f_bubblywave_a.nif'), self.assets))

    def test_a_different_model_file_does_not_reuse_it(self):
        baseline = {'parts': [{'assetId': 'cap-0'}]}
        self.assertFalse(declares_baseline_models(baseline, self.source('1/13/11300099_c_othercap.nif'), self.assets))

    def test_a_baseline_without_a_model_file_is_never_matched(self):
        self.assertFalse(declares_baseline_models({'parts': [{'assetId': 'decal'}]}, self.source(), self.assets))

    def test_extra_declared_parts_still_reuse_it(self):
        baseline = {'parts': [{'assetId': 'cap-0'}]}
        source = self.source('1/13/11300002_c_casualcap.nif', '1/18/wing.nif', None)
        self.assertTrue(declares_baseline_models(baseline, source, self.assets))


if __name__ == '__main__': unittest.main()
