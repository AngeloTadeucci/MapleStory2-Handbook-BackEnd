// Stage the explicit canonical inventory and build away from running applications.
import { createHash } from 'node:crypto';
import { mkdirSync, readdirSync, statSync, statfsSync, symlinkSync, readFileSync, writeFileSync, existsSync } from 'node:fs';
import { resolve, dirname, relative } from 'node:path';
import { spawn } from 'node:child_process';
const [work, ...options] = process.argv.slice(2);
if (!work || options.some(option => option !== '--stage-only'))
  throw Error('Usage: node build_wardrobe.mjs fresh-work-directory [--stage-only]');
const stageOnly = options.includes('--stage-only');
const frontend = process.env.HANDBOOK_FRONTEND;
if (!frontend) throw Error('HANDBOOK_FRONTEND is required');
const root = resolve(work), assets = resolve(frontend, 'static');
const models = process.env.HANDBOOK_MODELS_DIR ? resolve(process.env.HANDBOOK_MODELS_DIR) : resolve(assets, 'gltf');
if (existsSync(root)) throw Error('Use a fresh build directory');
const inventoryPath = resolve(models, 'model-files.json');
const inventory = JSON.parse(readFileSync(inventoryPath, 'utf8'));
if (inventory.version !== 1 || !Array.isArray(inventory.files)) throw Error('Generate the canonical model inventory before building');
const hash = path => createHash('sha256').update(readFileSync(path)).digest('hex');
const paths = new Set();
for (const file of inventory.files) {
  const path = file.path;
  if (typeof path !== 'string' || !path || path.startsWith('/') || path.includes('\\') || path.includes(':') ||
      path.split('/').includes('..') || /^(character-previews|simulator-release-\d+|.*preview[^/]*|gelo[^/]*)\//i.test(path) ||
      paths.has(path) || path === 'model-files.json') throw Error('Invalid, duplicate, or private model package path');
  const source = resolve(models, path);
  if (statSync(source).size !== file.bytes || hash(source) !== file.sha256) throw Error('Canonical asset changed: ' + path);
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
const required = stageOnly ? 128 * 1024 ** 2 : 8 * 1024 ** 3 + size * 2.5;
if (free < required) throw Error(`Build disk reserve failed: ${free} free, ${required} required for ${size} source bytes`);
mkdirSync(resolve(root, 'static/gltf'), { recursive: true });
symlinkSync(resolve(frontend, 'node_modules'), resolve(root, 'node_modules'), 'dir');
for (const entry of ordinary) symlinkSync(entry.path, resolve(root, 'static', entry.name), statSync(entry.path).isDirectory() ? 'dir' : 'file');
for (const path of paths) {
  const target = resolve(root, 'static/gltf', path);
  mkdirSync(dirname(target), { recursive: true });
  symlinkSync(resolve(models, path), target, 'file');
}
const env = { ...process.env, HANDBOOK_STATIC_DIR: resolve(root, 'static'), HANDBOOK_BUILD_DIR: resolve(root, 'output'),
  HANDBOOK_KIT_DIR: resolve(root, 'kit'), PUBLIC_MODELS_URL: '/gltf/' };
writeFileSync(resolve(root, 'build-input.json'), JSON.stringify({ inventorySha256: hash(inventoryPath), modelFiles: paths.size,
  sourceBytes: size, freeBytesBefore: free, stageOnly, staticDirectory: env.HANDBOOK_STATIC_DIR,
  outputDirectory: env.HANDBOOK_BUILD_DIR, kitDirectory: env.HANDBOOK_KIT_DIR, publicModelsUrl: '/gltf/' }, null, 2));
if (stageOnly) console.log(JSON.stringify({ stagedModelFiles: paths.size, sourceBytes: size, built: false }));
else {
  const child = spawn('pnpm', ['build'], { cwd: frontend, env, stdio: 'inherit' });
  const code = await new Promise((done, reject) => { child.on('error', reject); child.on('close', done); });
  if (code !== 0) process.exitCode = code ?? 1;
  else {
    const output = resolve(root, 'output/client/gltf');
    function walk(directory) {
      return readdirSync(directory).flatMap(name => {
        const path = resolve(directory, name);
        return statSync(path).isDirectory() ? walk(path) : [relative(output, path)];
      });
    }
    const actual = walk(output);
    if (actual.length !== paths.size || actual.some(path => !paths.has(path))) throw Error('Build model inventory differs from the allowlist');
    for (const file of inventory.files) if (hash(resolve(output, file.path)) !== file.sha256) throw Error('Packaged asset differs: ' + file.path);
    const after = statfsSync(root);
    writeFileSync(resolve(root, 'build-result.json'), JSON.stringify({ modelFiles: actual.length,
      hashesVerified: true, freeBytesAfter: after.bavail * after.bsize }, null, 2));
  }
}
