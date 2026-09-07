// Stage a bounded static tree and build away from the existing review process.
import { mkdirSync, readdirSync, statSync, statfsSync, symlinkSync, readFileSync, writeFileSync, existsSync } from 'node:fs';
import { resolve } from 'node:path';
import { spawn } from 'node:child_process';
const [work] = process.argv.slice(2);
if (!work) throw Error('Usage: node build_wardrobe.mjs fresh-work-directory');
const frontend = process.env.HANDBOOK_FRONTEND;
const release = JSON.parse(readFileSync(resolve(frontend, 'src/lib/outfits/simulator-release.json'), 'utf8')).directory;
if (!/^simulator-release-\d+$/.test(release)) throw Error('Invalid release');
const root = resolve(work), assets = resolve(frontend, 'static');
if (existsSync(root)) throw Error('Use a fresh build directory');
const selected = readdirSync(assets).filter(n => n !== 'gltf').map(n => ({ name: n, path: resolve(assets, n) }));
selected.push({ name: 'gltf/' + release, path: resolve(assets, 'gltf', release) });
function bytes(path) { const s = statSync(path); return s.isDirectory() ? readdirSync(path).reduce((n, child) => n + bytes(resolve(path, child)), 0) : s.size; }
const size = selected.reduce((n, entry) => n + bytes(entry.path), 0);
const disk = statfsSync(resolve(root, '..')), free = disk.bavail * disk.bsize;
// Client output, adapter output, compression, and the required free-space floor.
if (free < 8 * 1024 ** 3 + size * 2.5) throw Error(`Build disk reserve failed: ${free} free, ${size} source bytes`);
mkdirSync(resolve(root, 'static/gltf'), { recursive: true });
// SvelteKit evaluates generated server chunks during its build. Resolve the
// project's existing dependencies from this isolated build tree.
symlinkSync(resolve(frontend, 'node_modules'), resolve(root, 'node_modules'), 'dir');
for (const entry of selected) symlinkSync(entry.path, resolve(root, 'static', entry.name), statSync(entry.path).isDirectory() ? 'dir' : 'file');
const env = { ...process.env, HANDBOOK_STATIC_DIR: resolve(root, 'static'), HANDBOOK_BUILD_DIR: resolve(root, 'output'), HANDBOOK_KIT_DIR: resolve(root, 'kit'), PUBLIC_MODELS_URL: '/gltf/' };
writeFileSync(resolve(root, 'build-input.json'), JSON.stringify({ release, sourceBytes: size, freeBytesBefore: free, staticDirectory: env.HANDBOOK_STATIC_DIR, outputDirectory: env.HANDBOOK_BUILD_DIR, kitDirectory: env.HANDBOOK_KIT_DIR, publicModelsUrl: '/gltf/' }, null, 2));
const child = spawn('pnpm', ['build'], { cwd: frontend, env, stdio: 'inherit' });
const code = await new Promise((done, reject) => { child.on('error', reject); child.on('close', done); });
if (code !== 0) process.exitCode = code ?? 1;
else {
  const libraries = readdirSync(resolve(root, 'output/client/gltf'));
  if (libraries.length !== 1 || libraries[0] !== release) throw Error('Build includes an unexpected model library or private snapshot');
  const after = statfsSync(root);
  writeFileSync(resolve(root, 'build-result.json'), JSON.stringify({ release, libraries, freeBytesAfter: after.bavail * after.bsize }, null, 2));
}
