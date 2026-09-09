import { afterEach, test } from 'node:test';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { copyFileSync, mkdirSync, mkdtempSync, readFileSync, realpathSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import { basename, dirname, join } from 'node:path';
import { tmpdir } from 'node:os';
import { brotliCompressSync, gzipSync } from 'node:zlib';
import { verifyModelPackage } from './verify_model_package.mjs';

const roots = [];
afterEach(() => {
  for (const root of roots.splice(0)) {
    const target = realpathSync(root);
    assert.equal(dirname(target), realpathSync(tmpdir()));
    assert.ok(basename(target).startsWith('model-package-test-'));
    rmSync(target, { recursive: true, force: true });
  }
});
function fixture() {
  const root = mkdtempSync(join(tmpdir(), 'model-package-test-'));
  roots.push(root);
  const models = join(root, 'models'), output = join(root, 'output');
  mkdirSync(models); mkdirSync(output);
  const files = [];
  for (const [path, text] of [['catalog.json', '{"items":[]}'], ['asset.gltf', '{}']]) {
    const bytes = Buffer.from(text);
    writeFileSync(join(models, path), bytes);
    writeFileSync(join(output, path), bytes);
    files.push({ path, bytes: bytes.length, sha256: createHash('sha256').update(bytes).digest('hex') });
  }
  writeFileSync(join(models, 'model-files.json'), JSON.stringify({ version: 1, files }));
  copyFileSync(join(models, 'model-files.json'), join(output, 'model-files.json'));
  return { models, output };
}
test('verifies physical originals and both lossless transport encodings', () => {
  const { models, output } = fixture();
  for (const name of ['catalog.json', 'model-files.json']) {
    const bytes = readFileSync(join(output, name));
    writeFileSync(join(output, name + '.gz'), gzipSync(bytes));
    writeFileSync(join(output, name + '.br'), brotliCompressSync(bytes));
  }
  assert.deepEqual(verifyModelPackage(models, output), { modelFiles: 3, transportFiles: 4, totalFiles: 7,
    hashesVerified: true, transportHashesVerified: true, physicalFiles: true });
});
test('rejects a compressed response that decodes to different content', () => {
  const { models, output } = fixture();
  writeFileSync(join(output, 'catalog.json.gz'), gzipSync('{"items":{}}'));
  assert.throws(() => verifyModelPackage(models, output), /Compressed asset differs/);
});
test('rejects unlisted files and compression of unsupported extensions', () => {
  const { models, output } = fixture();
  writeFileSync(join(output, 'asset.gltf.gz'), gzipSync('{}'));
  assert.throws(() => verifyModelPackage(models, output), /Unexpected packaged asset/);
  rmSync(join(output, 'asset.gltf.gz'));
  writeFileSync(join(output, 'private.nif'), 'private');
  assert.throws(() => verifyModelPackage(models, output), /Unexpected packaged asset/);
});
test('checks the inventory itself and requires every original file', () => {
  const { models, output } = fixture();
  writeFileSync(join(output, 'model-files.json'), '{}');
  assert.throws(() => verifyModelPackage(models, output), /Packaged asset differs: model-files.json/);
  copyFileSync(join(models, 'model-files.json'), join(output, 'model-files.json'));
  rmSync(join(output, 'asset.gltf'));
  assert.throws(() => verifyModelPackage(models, output), /Missing packaged asset/);
});
test('requires actual files in the production output', { skip: process.platform === 'win32' }, () => {
  const { models, output } = fixture();
  rmSync(join(output, 'asset.gltf'));
  symlinkSync(join(models, 'asset.gltf'), join(output, 'asset.gltf'));
  assert.throws(() => verifyModelPackage(models, output), /non-physical file/);
});
