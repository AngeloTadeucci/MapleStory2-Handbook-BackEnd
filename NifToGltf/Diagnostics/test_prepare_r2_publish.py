import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from prepare_r2_publish import prepare, finalize, phase, REMOTE


class PublishTests(unittest.TestCase):
    def fixture(self, root):
        models=root/'models'; models.mkdir()
        records=[]
        for name,data in [('keep.png',b'same'),('replace.gltf',b'new'),('new/model.gltf',b'new model')]:
            path=models/name; path.parent.mkdir(exist_ok=True); path.write_bytes(data)
            records.append(dict(path=name,bytes=len(data),sha256=hashlib.sha256(data).hexdigest()))
        (models/'model-files.json').write_text(json.dumps(dict(version=1,files=records)))
        backup=root/'backup'; (backup/'objects').mkdir(parents=True)
        (backup/'objects/replace.gltf').write_bytes(b'old')
        (backup/'backup.json').write_text(json.dumps(dict(complete=True,remote=REMOTE,files=[
            dict(path='replace.gltf',bytes=3,sha256=hashlib.sha256(b'old').hexdigest(),remote={'Metadata':{'content-type':'application/octet-stream'}})])))
        listing=root/'remote.json'
        listing.write_text(json.dumps([dict(Path=name,Size=len(data),Hashes={'md5':hashlib.md5(data).hexdigest()})
            for name,data in [('keep.png',b'same'),('replace.gltf',b'old'),('untouched.bin',b'unrelated')]]))
        return models,listing,backup

    def test_delta_preserves_unrelated_objects_and_verifies_replacement_backup(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp); models,listing,backup=self.fixture(root)
            plan=prepare(models,listing,backup,root/'out')
            self.assertEqual(plan['unchanged'],1)
            self.assertEqual(plan['actions'],{'replace':1,'create':2})
            self.assertEqual(plan['rollbackFiles'],1)
            self.assertEqual(plan['missingBackup'],[])
            self.assertFalse(plan['deleteRemoteObjects'])
            self.assertFalse((root/'out/assets/keep.png').exists())
            self.assertEqual((root/'out/rollback/objects/replace.gltf').read_bytes(),b'old')
            self.assertEqual((models/'replace.gltf').read_bytes(),b'new')
            self.assertEqual(phase('a/texture.png'),10)
            self.assertLess(phase('a/a.gltf'),phase('native-manifest.json'))
            self.assertLess(phase('native-manifest.json'),phase('simulator-catalog.json'))
            self.assertLess(phase('simulator-catalog.json'),phase('model-files.json'))
            current=json.loads(listing.read_text())[1]
            current['Metadata']={'content-type':'model/gltf+json','cache-control':'old policy'}
            metadata=root/'metadata.json'; metadata.write_text(json.dumps([current]))
            final=finalize(root/'out',metadata)
            self.assertTrue(final['prepared'])
            self.assertTrue(final['metadataFresh'])
            self.assertEqual(final['replacementMetadataSha256'],hashlib.sha256((root/'out/replacement-metadata.json').read_bytes()).hexdigest())
            saved=json.loads((root/'out/rollback/backup.json').read_text())
            self.assertEqual(saved['files'][0]['remote']['Metadata'],current['Metadata'])
            self.assertIn('--dry-run',(root/'out/preview-upload.ps1').read_text())
            current['Hashes']['md5']='0'*32; metadata.write_text(json.dumps([current]))
            with self.assertRaises(ValueError): finalize(root/'out',metadata)
            self.assertFalse(json.loads((root/'out/publish-plan.json').read_text())['prepared'])

    def test_remote_drift_does_not_claim_rollback_is_current(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp); models,listing,backup=self.fixture(root)
            remote=json.loads(listing.read_text()); remote[1]['Hashes']['md5']='0'*32
            listing.write_text(json.dumps(remote))
            plan=prepare(models,listing,backup,root/'out')
            self.assertEqual(plan['missingBackup'],['replace.gltf'])
            self.assertEqual(plan['rollbackFiles'],0)

    def test_changed_local_bytes_and_private_paths_are_rejected(self):
        for path in ['../outside.png','simulator-release-15/private.png','a\nextra.png']:
            with tempfile.TemporaryDirectory() as temp:
                root=Path(temp); models,listing,backup=self.fixture(root)
                manifest=json.loads((models/'model-files.json').read_text())
                manifest['files'][0]['path']=path
                (models/'model-files.json').write_text(json.dumps(manifest))
                with self.assertRaises(ValueError): prepare(models,listing,backup,root/'out')
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp); models,listing,backup=self.fixture(root)
            (models/'keep.png').write_bytes(b'edited')
            with self.assertRaises(ValueError): prepare(models,listing,backup,root/'out')


if __name__=='__main__': unittest.main()
