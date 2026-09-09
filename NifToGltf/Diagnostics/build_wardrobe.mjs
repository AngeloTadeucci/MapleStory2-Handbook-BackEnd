// Stage the explicit canonical inventory and build away from running applications.
import { createHash } from 'node:crypto';
import { lstatSync, mkdirSync, readdirSync, statSync, statfsSync, symlinkSync, readFileSync, writeFileSync, existsSync } from 'node:fs';
import { resolve, dirname, sep } from 'node:path';
import { spawn } from 'node:child_process';
import { verifyModelPackage } from './verify_model_package.mjs';
const [work, ...options] = process.argv.slice(2);
if (!work || options.some(option => option !== '--stage-only' && !option.startsWith('--assets-from=')) ||
    options.filter(option => option.startsWith('--assets-from=')).length > 1)
  throw Error('Usage: node build_wardrobe.mjs fresh-work-directory [--stage-only | --assets-from=verified-build-directory]');
const stageOnly = options.includes('--stage-only');
const reuseOption = options.find(option => option.startsWith('--assets-from='));
if (stageOnly && reuseOption) throw Error('Asset reuse requires a production build');
const assetsBuild = reuseOption ? resolve(reuseOption.slice('--assets-from='.length)) : undefined;
const assetsFrom = assetsBuild ? resolve(assetsBuild, 'output/client/gltf') : undefined;
const frontend = process.env.HANDBOOK_FRONTEND;
if (!frontend) throw Error('HANDBOOK_FRONTEND is required');
const root = resolve(work), assets = resolve(frontend, 'static');
const models = process.env.HANDBOOK_MODELS_DIR ? resolve(process.env.HANDBOOK_MODELS_DIR) : resolve(assets, 'gltf');
if (existsSync(root)) throw Error('Use a fresh build directory');
const modelSource = assetsFrom ?? models;
if (root.startsWith(modelSource + sep)) throw Error('Build outside the immutable model source');
const inventoryPath = resolve(models, 'model-files.json');
const inventory = JSON.parse(readFileSync(inventoryPath, 'utf8'));
if (inventory.version !== 1 || !Array.isArray(inventory.files)) throw Error('Generate the canonical model inventory before building');
const hash = path => createHash('sha256').update(readFileSync(path)).digest('hex');
if (assetsBuild) {
  const prior = JSON.parse(readFileSync(resolve(assetsBuild, 'build-result.json'), 'utf8'));
  const input = JSON.parse(readFileSync(resolve(assetsBuild, 'build-input.json'), 'utf8'));
  if (!prior.hashesVerified || !prior.physicalFiles || input.inventorySha256 !== hash(inventoryPath) ||
      hash(resolve(assetsFrom, 'model-files.json')) !== input.inventorySha256)
    throw Error('Asset reuse requires a verified physical build of this inventory');
}
const paths = new Set();
for (const file of inventory.files) {
  const path = file.path;
  if (typeof path !== 'string' || !path || path.startsWith('/') || path.includes('\\') || path.includes(':') ||
      path.split('/').includes('..') || /^(character-previews|simulator-release-\d+|.*preview[^/]*|gelo[^/]*)\//i.test(path) ||
      paths.has(path) || path === 'model-files.json') throw Error('Invalid, duplicate, or private model package path');
  const source = resolve(modelSource, path);
  if (assetsFrom && !lstatSync(source).isFile()) throw Error('Reuse requires physical model files: ' + path);
  if (statSync(source).size !== file.bytes || (!assetsFrom && hash(source) !== file.sha256)) throw Error('Canonical asset changed: ' + path);
  paths.add(path);
}
paths.add('model-files.json');
const ordinary = readdirSync(assets).filter(name => name !== 'gltf').map(name => ({ name, path: resolve(assets, name) }));
function bytes(path) {
  const stat = statSync(path);
  return stat.isDirectory() ? readdirSync(path).reduce((count, child) => count + bytes(resolve(path, child)), 0) : stat.size;
}
const size = ordinary.reduce((count, entry) => count + bytes(entry.path), 0) +
  inventory.files.reduce((count, file) => count + file.bytes, 0) + statSync(inventoryPath).size;
const disk = statfsSync(dirname(root)), free = disk.bavail * disk.bsize;
const required = stageOnly ? 128 * 1024 ** 2 : 8 * 1024 ** 3 + (assetsFrom ? 0 : size * 2.5);
if (free < required) throw Error(`Build disk reserve failed: ${free} free, ${required} required for ${size} source bytes`);
mkdirSync(resolve(root, 'static/gltf'), { recursive: true });
symlinkSync(resolve(frontend, 'node_modules'), resolve(root, 'node_modules'), 'dir');
for (const entry of ordinary) symlinkSync(entry.path, resolve(root, 'static', entry.name), statSync(entry.path).isDirectory() ? 'dir' : 'file');
for (const path of paths) {
  const target = resolve(root, 'static/gltf', path);
  mkdirSync(dirname(target), { recursive: true });
  symlinkSync(resolve(modelSource, path), target, 'file');
}
const env = { ...process.env, HANDBOOK_STATIC_DIR: resolve(root, 'static'), HANDBOOK_BUILD_DIR: resolve(root, 'output'),
  HANDBOOK_KIT_DIR: resolve(root, 'kit'), HANDBOOK_LINK_MODELS: assetsFrom ? '1' : '0',
  HANDBOOK_REUSE_MODELS_DIR: assetsFrom ?? '',
  PUBLIC_MODELS_URL: '/gltf/', PUBLIC_IMAGES_URL: '/' };
writeFileSync(resolve(root, 'build-input.json'), JSON.stringify({ inventorySha256: hash(inventoryPath), modelFiles: paths.size,
  sourceBytes: size, freeBytesBefore: free, stageOnly, staticDirectory: env.HANDBOOK_STATIC_DIR,
  outputDirectory: env.HANDBOOK_BUILD_DIR, kitDirectory: env.HANDBOOK_KIT_DIR, publicModelsUrl: '/gltf/',
  publicImagesUrl: '/', assetsFrom: assetsFrom ?? null }, null, 2));
if (stageOnly) console.log(JSON.stringify({ stagedModelFiles: paths.size, sourceBytes: size, built: false }));
else {
  const child = spawn('pnpm', ['build'], { cwd: frontend, env, stdio: 'inherit' });
  const code = await new Promise((done, reject) => { child.on('error', reject); child.on('close', done); });
  if (code !== 0) process.exitCode = code ?? 1;
  else {
    const output = resolve(root, 'output/client/gltf');
    const verified = verifyModelPackage(models, output);
    const after = statfsSync(root);
    writeFileSync(resolve(root, 'build-result.json'), JSON.stringify({ ...verified,
      freeBytesAfter: after.bavail * after.bsize }, null, 2));
  }
}
