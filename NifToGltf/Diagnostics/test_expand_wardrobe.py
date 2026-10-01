import unittest
from expand_wardrobe import superseded_baseline


def asset(identity, source, body='male', slot='HR', **extra):
    return {'id': identity, 'input': f'WardrobeSources/Item/0/02/{source}.nif', 'bodyVariant': body, 'slot': slot,
            'uri': f'wardrobe/{identity}.gltf', **extra}


class SupersededBaselineTests(unittest.TestCase):
    def test_baseline_reproduced_by_a_new_conversion_is_dropped(self):
        assets = {a['id']: a for a in [asset('wardrobe-old', 'hair_a'), asset('wardrobe-new', 'HAIR_A')]}
        self.assertEqual(superseded_baseline({'wardrobe-old'}, assets, {'wardrobe-new'}), ['wardrobe-old'])

    def test_referenced_baseline_is_kept(self):
        assets = {a['id']: a for a in [asset('wardrobe-old', 'hair_a'), asset('wardrobe-new', 'hair_a')]}
        self.assertEqual(superseded_baseline({'wardrobe-old'}, assets, {'wardrobe-old'}), [])

    def test_different_body_placement_or_form_is_not_a_duplicate(self):
        assets = {a['id']: a for a in [
            asset('wardrobe-old-f', 'hair_a', body='female'),
            asset('wardrobe-old-lh', 'blade', slot='OH', hand='LH'),
            asset('wardrobe-old-c', 'hair_c', alternateOf='WardrobeSources/Item/0/02/hair_a.nif'),
            asset('wardrobe-new', 'hair_a'),
            asset('wardrobe-new-rh', 'blade', slot='OH', hand='RH'),
            asset('wardrobe-new-c', 'hair_c')]}
        baseline = {'wardrobe-old-f', 'wardrobe-old-lh', 'wardrobe-old-c'}
        self.assertEqual(superseded_baseline(baseline, assets, set()), [])

    def test_non_wardrobe_baseline_assets_are_never_touched(self):
        assets = {a['id']: a for a in [asset('f_body', 'f_body'), asset('wardrobe-new', 'f_body')]}
        self.assertEqual(superseded_baseline({'f_body'}, assets, set()), [])


if __name__ == '__main__': unittest.main()
