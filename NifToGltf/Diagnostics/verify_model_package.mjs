import { createHash } from 'node:crypto';
import { lstatSync, readFileSync, readdirSync, statfsSync, writeFileSync } from 'node:fs';
import { extname, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { brotliDecompressSync, gunzipSync } from 'node:zlib';

const compressible = new Set(['.html', '.js', '.mjs', '.json', '.css', '.svg', '.xml', '.wasm']);
const hash = bytes => createHash('sha256').update(bytes).digest('hex');

export function verifyModelPackage(models, output) {
  const inventoryBytes = readFileSync(resolve(models, 'model-files.json'));
  const inventory = JSON.parse(inventoryBytes);
  if (inventory.version !== 1 || !Array.isArray(inventory.files)) throw Error('Invalid model inventory');
  const expected = new Map();
  for (const file of inventory.files) {
    const path = file.path;
    if (typeof path !== 'string' || !path || path.startsWith('/') || path.includes('\\') ||
        path.includes(':') || path.split('/').includes('..') || path === 'model-files.json' ||
        /^(character-previews|simulator-release-\d+|.*preview[^/]*|gelo[^/]*)\//i.test(path) || expected.has(path))
      throw Error('Invalid, duplicate, or private model package path');
    expected.set(path, file);
  }
  expected.set('model-files.json', { bytes: inventoryBytes.length, sha256: hash(inventoryBytes) });
  const actual = new Set();
  function walk(directory) {
    for (const name of readdirSync(directory)) {
      const path = resolve(directory, name), stat = lstatSync(path);
      if (stat.isDirectory()) walk(path);
      else if (stat.isFile()) actual.add(relative(output, path).split(sep).join('/'));
      else throw Error('Production model package contains a non-physical file: ' + path);
    }
  }
  walk(output);
  for (const [path, record] of expected) {
    if (!actual.has(path)) throw Error('Missing packaged asset: ' + path);
    const bytes = readFileSync(resolve(output, path));
    if (bytes.length !== record.bytes || hash(bytes) !== record.sha256) throw Error('Packaged asset differs: ' + path);
  }
  let transportFiles = 0;
  for (const path of actual) {
    if (expected.has(path)) continue;
    const encoding = extname(path), original = path.slice(0, -encoding.length);
    const record = expected.get(original);
    if (!record || !['.gz', '.br'].includes(encoding) || !compressible.has(extname(original)))
      throw Error('Unexpected packaged asset: ' + path);
    const compressed = readFileSync(resolve(output, path));
    const decode = encoding === '.gz' ? gunzipSync : brotliDecompressSync;
    const bytes = decode(compressed, { maxOutputLength: record.bytes + 1 });
    if (bytes.length !== record.bytes || hash(bytes) !== record.sha256) throw Error('Compressed asset differs: ' + path);
    transportFiles++;
  }
  return { modelFiles: expected.size, transportFiles, totalFiles: actual.size,
    hashesVerified: true, transportHashesVerified: true, physicalFiles: true };
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const [models, output, report] = process.argv.slice(2);
  if (!models || !output || !report) throw Error('Usage: verify_model_package.mjs models output-gltf report.json');
  const result = verifyModelPackage(resolve(models), resolve(output));
  const disk = statfsSync(output);
  result.freeBytesAfter = disk.bavail * disk.bsize;
  writeFileSync(report, JSON.stringify(result, null, 2));
  console.log(JSON.stringify(result));
}
