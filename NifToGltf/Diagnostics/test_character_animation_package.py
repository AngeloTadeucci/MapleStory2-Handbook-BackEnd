from copy import deepcopy
import unittest

from package_character_animations import preserve_body_appearance


class BodyAppearanceTests(unittest.TestCase):
    def setUp(self):
        self.original = {
            'nodes': [], 'meshes': [], 'skins': [], 'textures': [],
            'materials': [{'name': 'body', 'extras': {'nifShader': 'skin'}}],
            'images': [{'name': 'skin', 'uri': 'data:reviewed'}],
        }
        self.rebuilt = deepcopy(self.original)

    def test_new_render_metadata_does_not_change_body_appearance(self):
        self.rebuilt['materials'][0]['extras']['nifRenderState'] = {'depthFlags': 15}
        self.rebuilt['images'][0]['uri'] = 'data:reconverted'
        preserve_body_appearance(self.rebuilt, self.original)
        self.assertEqual(self.rebuilt, self.original)

    def test_other_material_changes_are_rejected(self):
        self.rebuilt['materials'][0]['extras']['nifShader'] = 'other'
        with self.assertRaisesRegex(ValueError, 'materials'):
            preserve_body_appearance(self.rebuilt, self.original)

    def test_existing_render_state_changes_are_rejected(self):
        self.original['materials'][0]['extras']['nifRenderState'] = {'depthFlags': 15}
        self.rebuilt['materials'][0]['extras']['nifRenderState'] = {'depthFlags': 13}
        with self.assertRaisesRegex(ValueError, 'materials'):
            preserve_body_appearance(self.rebuilt, self.original)

    def test_geometry_changes_are_rejected(self):
        self.rebuilt['nodes'] = [{'name': 'changed'}]
        with self.assertRaisesRegex(ValueError, 'nodes'):
            preserve_body_appearance(self.rebuilt, self.original)


if __name__ == '__main__':
    unittest.main()
