import unittest
import base64
import os
import tempfile
import json
import subprocess
from unittest.mock import patch
from pathlib import Path
from expand_wardrobe import recover_baseline_hair_forms
from migration_inventory import model_references, safe_path, sha, write
from migration_recovery import restore
from package_models import layout, retain_legacy
from migration_standalone import prepare as prepare_standalone
from migration_collect import collect
from migration_inventory_jobs import prepare as prepare_inventory


class MigrationTests(unittest.TestCase):
    def test_collection_rejects_a_batch_stopped_at_its_disk_reserve(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            write(root / 'work/jobs.json', [{'model': 'pending'}])
            write(root / 'work/results.json', [])
            write(root / 'work/source-failures.json', [])
            with self.assertRaisesRegex(ValueError, 'Incomplete conversion workspace'):
                collect([root / 'work'], root / 'output')
            self.assertFalse((root / 'output').exists())

    def test_inventory_kfm_alias_keeps_controller_name_and_declared_model(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'alias.kfm').write_bytes(b'controller')
            (root / 'shared.nif').write_bytes(b'mesh')
            write(root / 'mapping.json', [{'model': 'alias', 'sources': [{'path': 'alias.kfm'}],
                                           'databaseReferences': [], 'publishedFiles': []}])
            write(root / 'manifest.json', {'assets': []})
            write(root / 'coverage.json', {'history': {}, 'unresolved': []})
            with patch('migration_inventory_jobs.read_kfm', return_value={'model': 'shared.nif', 'clips': []}):
                prepare_inventory(root, root / 'mapping.json', root / 'manifest.json', root / 'coverage.json', root / 'jobs')
            job = json.loads((root / 'jobs/jobs.json').read_text())[0]
            self.assertEqual(job['plan'], {'id': 'alias', 'input': 'shared.nif', 'kfm': 'alias.kfm',
                                          'clips': ['all'], 'output': 'alias/alias.gltf'})

    def test_inventory_includes_published_only_models_and_excludes_prior_attempts(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'published.nif').write_text('original geometry')
            write(root / 'mapping.json', [
                {'model': name, 'sources': [{'path': 'published.nif'}], 'databaseReferences': [],
                 'publishedFiles': [name + '/' + name + '.gltf']} for name in ['published', 'attempted']])
            write(root / 'manifest.json', {'assets': []})
            write(root / 'coverage.json', {'history': {'attempted': []}, 'unresolved': []})
            prepare_inventory(root, root / 'mapping.json', root / 'manifest.json', root / 'coverage.json', root / 'jobs')
            jobs = json.loads((root / 'jobs/jobs.json').read_text())
            self.assertEqual([job['model'] for job in jobs], ['published'])
            self.assertEqual(jobs[0]['plan']['input'], 'published.nif')
            self.assertEqual(jobs[0]['sources']['published.nif'], sha(root / 'published.nif'))

    def test_collection_preserves_success_across_failed_retry_and_reports_both(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / 'model.gltf'
            source.write_text('{}')
            good = {'model': 'model', 'status': 'converted-unreviewed', 'file': str(source),
                    'sha256': sha(source), 'asset': {'id': 'model', 'uri': 'model/model.gltf'}}
            bad = {'model': 'model', 'status': 'unsupported-feature', 'asset': None, 'errors': ['unsupported']}
            for name, row in [('old', good), ('retry', bad)]:
                write(root / name / 'results.json', [row])
                write(root / name / 'source-failures.json', [])
            collect([root / 'old', root / 'retry'], root / 'output')
            report = json.loads((root / 'output/conversion-coverage.json').read_text())
            self.assertEqual(report['convertedUnreviewed'], 1)
            self.assertEqual(report['unresolved'], [])
            self.assertEqual(len(report['history']['model']), 2)
            self.assertEqual((root / 'output/model/model.gltf').read_text(), '{}')

    def test_collection_reports_latest_failure_after_a_controller_fix(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for name, error in [('old', 'duplicate sequence'), ('retry', 'unsupported material')]:
                write(root / name / 'results.json', [{'model': 'model', 'status': 'unsupported-feature',
                                                      'asset': None, 'errors': [error]}])
                write(root / name / 'source-failures.json', [])
            collect([root / 'old', root / 'retry'], root / 'output')
            report = json.loads((root / 'output/conversion-coverage.json').read_text())
            self.assertEqual(report['unresolved'][0]['errors'], ['unsupported material'])

    def test_invalidated_conversion_is_not_restored_after_a_failed_retry(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / 'wrong.gltf'
            source.write_text('{}')
            write(root / 'old/results.json', [{'model': 'model', 'status': 'converted-unreviewed',
                  'file': str(source), 'sha256': sha(source), 'asset': {'id': 'model', 'uri': 'model/model.gltf'}}])
            write(root / 'old/source-failures.json', [])
            write(root / 'retry/invalidates.json', {'model': 'Previous interpolation was incorrect'})
            write(root / 'retry/results.json', [{'model': 'model', 'status': 'unsupported-feature', 'asset': None}])
            write(root / 'retry/source-failures.json', [])
            collect([root / 'old', root / 'retry'], root / 'output')
            self.assertFalse((root / 'output/model/model.gltf').exists())
            report = json.loads((root / 'output/conversion-coverage.json').read_text())
            self.assertEqual(report['convertedUnreviewed'], 0)
            self.assertEqual(report['unresolved'][0]['model'], 'model')

    def test_legacy_fallback_retains_clips_and_dependencies_without_replacing_native(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            mirror, output = root / 'mirror', root / 'output'
            (mirror / 'npc').mkdir(parents=True)
            (mirror / 'shared').mkdir()
            (mirror / 'shared/mesh.bin').write_bytes(b'buffer')
            write(mirror / 'npc/npc.gltf', {'legacy': True})
            write(mirror / 'npc/idle.gltf', {'buffers': [{'uri': '../shared/mesh.bin'}]})
            write(mirror / 'published-only/published-only.gltf', {})
            write(mirror / 'character-previews/private.gltf', {})
            write(output / 'npc/npc.gltf', {'native': True})
            result = retain_legacy(mirror, output, ['npc', 'missing'])
            self.assertEqual(json.loads((output / 'npc/npc.gltf').read_text()), {'native': True})
            self.assertEqual((output / 'shared/mesh.bin').read_bytes(), b'buffer')
            self.assertTrue((output / 'npc/idle.gltf').is_file())
            self.assertTrue((output / 'published-only/published-only.gltf').is_file())
            self.assertFalse((output / 'character-previews').exists())
            self.assertEqual(result['missingFolders'], ['missing'])

    def test_standalone_keeps_whole_source_and_rejects_distinct_sources(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            resources = root / 'resources'
            resources.mkdir()
            (resources / 'dress.nif').write_bytes(b'whole dress with top and pants')
            (resources / 'other.nif').write_bytes(b'different original geometry')
            assets = [{'id': name, 'input': path, 'clips': [], 'slot': slot}
                      for name, path, slot in [('top', 'dress.nif', 'CL'), ('pants', 'dress.nif', 'PA')]]
            write(root / 'manifest.json', {'assets': assets})
            write(root / 'variants.json', {'models': [{'model': 'dress', 'defaultAssetId': None, 'variants': ['top', 'pants']}]})
            prepare_standalone(resources, root / 'manifest.json', root / 'variants.json', root / 'whole')
            job = json.loads((root / 'whole/jobs.json').read_text())[0]
            self.assertEqual(job['plan'], {'id': 'dress', 'input': 'dress.nif', 'output': 'dress/dress.gltf'})
            self.assertEqual(job['sources'], {'dress.nif': sha(resources / 'dress.nif')})
            assets[1]['input'] = 'other.nif'
            write(root / 'manifest.json', {'assets': assets})
            prepare_standalone(resources, root / 'manifest.json', root / 'variants.json', root / 'distinct')
            self.assertEqual(json.loads((root / 'distinct/jobs.json').read_text()), [])
            self.assertIn('Distinct original', (root / 'distinct/source-failures.json').read_text())

    def test_standalone_does_not_silently_drop_external_animation(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'dress.nif').write_bytes(b'geometry')
            (root / 'dress_idle.kf').write_bytes(b'animation')
            write(root / 'manifest.json', {'assets': [{'id': 'top', 'input': 'dress.nif', 'clips': []}]})
            write(root / 'variants.json', {'models': [{'model': 'dress', 'defaultAssetId': None, 'variants': ['top']}]})
            prepare_standalone(root, root / 'manifest.json', root / 'variants.json', root / 'output')
            self.assertEqual(json.loads((root / 'output/jobs.json').read_text()), [])
            self.assertIn('External animation', (root / 'output/source-failures.json').read_text())

    def test_build_staging_checks_hashes_and_excludes_unlisted_files(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            models = root / 'candidate'
            models.mkdir(parents=True)
            (root / 'frontend/static').mkdir(parents=True)
            (root / 'frontend/node_modules').mkdir()
            (models / 'model.gltf').write_text('model')
            (models / 'private.gltf').write_text('private')
            write(models / 'model-files.json', {'version': 1, 'files': [
                {'path': 'model.gltf', 'bytes': 5, 'sha256': sha(models / 'model.gltf')}]})
            script = Path(__file__).with_name('build_wardrobe.mjs')
            env = dict(os.environ, HANDBOOK_FRONTEND=str(root / 'frontend'), HANDBOOK_MODELS_DIR=str(models))
            result = subprocess.run(['node', str(script), str(root / 'stage'), '--stage-only'], env=env, capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertTrue((root / 'stage/static/gltf/model.gltf').is_file())
            self.assertFalse((root / 'stage/static/gltf/private.gltf').exists())
            (models / 'model.gltf').write_text('other')
            result = subprocess.run(['node', str(script), str(root / 'changed'), '--stage-only'], env=env, capture_output=True, text=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn('Canonical asset changed', result.stderr)
            self.assertFalse((root / 'changed').exists())

    def test_case_insensitive_duplicate_asset_ids_are_rejected(self):
        with self.assertRaisesRegex(ValueError, 'Duplicate native asset identity'):
            layout([{'id': 'Model', 'input': 'male.nif'}, {'id': 'MODEL', 'input': 'female.nif'}])

    def test_unattached_source_identity_is_canonical_beside_equipment_variants(self):
        assets = [{'id': 'f_body', 'input': 'female/f_body.nif', 'bodyVariant': 'female'},
                  {'id': 'face-preset', 'input': 'female/f_body.nif', 'bodyVariant': 'female', 'attach': 'Head'}]
        result, choices = layout(assets, {'face-preset'})
        self.assertEqual(choices[0]['defaultAssetId'], 'f_body')
        self.assertEqual(next(a['uri'] for a in result if a['id'] == 'f_body'), 'f_body/f_body.gltf')

    def test_explicit_npc_alias_preserves_controller_identity(self):
        assets = [{'id': 'controller-a', 'model': 'controller-a', 'input': 'shared.nif'},
                  {'id': 'controller-b', 'model': 'controller-b', 'input': 'shared.nif'}]
        result, choices = layout(assets)
        self.assertEqual({a['uri'] for a in result}, {'controller-a/controller-a.gltf', 'controller-b/controller-b.gltf'})
        self.assertTrue(all(c['defaultAssetId'] for c in choices))

    @unittest.skipUnless(hasattr(os, 'setxattr'), 'Filesystem metadata support is required')
    def test_restore_uses_each_objects_saved_metadata_after_deduplication(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / 'backup/objects/model.gltf'
            source.parent.mkdir(parents=True)
            source.write_text('identical bytes')
            os.setxattr(source, 'user.content-type', b'wrong-peer-metadata')
            record = {'path': 'model.gltf', 'bytes': source.stat().st_size, 'sha256': sha(source),
                      'xattrsBase64': {'user.content-type': base64.b64encode(b'model/gltf+json').decode()},
                      'mtimeNs': 1234567890000000000}
            write(root / 'backup/backup.json', {'complete': True, 'files': [record]})
            restore(root / 'backup', root / 'restore')
            target = root / 'restore/model.gltf'
            self.assertEqual(os.getxattr(target, 'user.content-type'), b'model/gltf+json')
            self.assertEqual(target.stat().st_mtime_ns, record['mtimeNs'])

    def test_canonical_body_choice_is_explicit_and_independent_of_order(self):
        assets = [{'id': body, 'input': 'Item/hat.nif', 'bodyVariant': body} for body in ['female', 'male']]
        first, choices = layout(assets)
        second, _ = layout(list(reversed(assets)))
        self.assertEqual(first, second)
        self.assertEqual(choices[0]['defaultAssetId'], 'male')
        self.assertEqual({a['id']: a['uri'] for a in first}, {'male': 'hat/hat.gltf', 'female': 'hat/hat-female.gltf'})

    def test_ambiguous_attachments_keep_distinct_paths_without_default(self):
        assets = [{'id': hand, 'input': 'Item/weapon.nif', 'bodyVariant': 'male', 'slot': hand} for hand in ['RH', 'LH']]
        result, choices = layout(assets)
        self.assertIsNone(choices[0]['defaultAssetId'])
        self.assertEqual(len({a['uri'] for a in result}), 2)
        self.assertTrue(all(not a['standalone'] for a in result))

    def test_primary_catalog_geometry_wins_over_unused_baseline_duplicate(self):
        assets = [{'id': name, 'input': 'Item/hair_a.nif', 'bodyVariant': 'male'} for name in ['unused', 'equipped']]
        result, choices = layout(assets, {'equipped'})
        self.assertEqual(choices[0]['defaultAssetId'], 'equipped')
        self.assertEqual(next(a['uri'] for a in result if a['id'] == 'equipped'), 'hair_a/hair_a.gltf')

    def test_baseline_reuse_recovers_both_forms_without_claiming_visual_review(self):
        entry = {'slots': ['HR'], 'hairForms': {}, 'availability': 'verified', 'parts': [{'assetId': 'loose'}]}
        recover_baseline_hair_forms(entry, {'c': ['cap'], 'd': ['hat']}, {'cap': {}, 'hat': {}}, {}, {})
        self.assertEqual(entry['hairForms'], {'c': ['cap'], 'd': ['hat']})
        self.assertEqual(entry['availability'], 'preview')
        self.assertEqual(entry['parts'], [{'assetId': 'loose'}])

    def test_failed_alternate_keeps_working_loose_hair_and_records_failure(self):
        entry = {'slots': ['HR'], 'hairForms': {}, 'availability': 'verified'}
        recover_baseline_hair_forms(entry, {'c': ['missing'], 'd': ['animated']}, {'animated': {}},
                                    {'missing': 'Missing texture'}, {'animated': 'Unbound target'})
        self.assertEqual(entry['hairForms'], {})
        self.assertEqual(entry['availability'], 'verified')
        self.assertEqual(entry['hairFormFailures'], {'c': ['Missing texture'], 'd': ['Unbound target']})

    def test_existing_reviewed_alternate_is_not_replaced(self):
        entry = {'slots': ['HR'], 'hairForms': {'c': ['reviewed']}, 'availability': 'verified'}
        recover_baseline_hair_forms(entry, {'c': ['new']}, {'new': {}}, {}, {})
        self.assertEqual(entry['hairForms']['c'], ['reviewed'])
        self.assertEqual(entry['availability'], 'verified')

    def test_shared_database_models_retain_all_identities(self):
        result = model_references({'items': [{'id': 1, 'kfms': '["Shared"]'}, {'id': 2, 'kfms': ['shared']}],
                                   'npcs': [{'id': 3, 'kfm': 'shared'}]})
        self.assertEqual([r['id'] for r in result['shared']], [1, 2, 3])

    def test_unsafe_remote_paths_are_rejected(self):
        for value in ['../secret', '/absolute', 'a/../b', 'a\\b', 'https://host/a', '']:
            with self.subTest(value=value), self.assertRaises(ValueError):
                safe_path(value)
        self.assertEqual(str(safe_path('model/model.gltf')), 'model/model.gltf')

    def test_restore_checks_all_hashes_before_creating_destination(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / 'backup/objects/model/model.gltf'
            source.parent.mkdir(parents=True)
            source.write_text('original')
            record = {'path': 'model/model.gltf', 'bytes': source.stat().st_size, 'sha256': sha(source)}
            write(root / 'backup/backup.json', {'complete': True, 'files': [record]})
            source.write_text('tampered')
            with self.assertRaisesRegex(ValueError, 'Recovery bytes changed'):
                restore(root / 'backup', root / 'restore')
            self.assertFalse((root / 'restore').exists())
            source.write_text('original')
            restore(root / 'backup', root / 'restore')
            self.assertEqual((root / 'restore/model/model.gltf').read_text(), 'original')
            with self.assertRaisesRegex(ValueError, 'must not exist'):
                restore(root / 'backup', root / 'restore')


if __name__ == '__main__':
    unittest.main()
