// Run every geometry assertion in bounded processes; retain complete per-batch reports.
import { spawn } from 'node:child_process';
import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync, mkdirSync, existsSync } from 'node:fs';
import { resolve } from 'node:path';
const [library, output] = process.argv.slice(2);
if (!library || !output) throw Error('Usage: node verify_wardrobe.mjs library-directory output-directory');
const frontend = process.env.HANDBOOK_FRONTEND;
const assets = JSON.parse(readFileSync(resolve(library, 'native-manifest.json'), 'utf8')).assets.filter(a => a.skeleton);
const hash = createHash('sha256');
for (const path of [resolve(library, 'release-inventory.json'), ...['tests/simulatorLibrary.test.ts', 'src/lib/outfits/sharedSkeleton.ts', 'pnpm-lock.yaml'].map(p => resolve(frontend, p))]) hash.update(readFileSync(path));
const fingerprint = hash.digest('hex');
mkdirSync(output, { recursive: true });
let cursor = 0;
const records = [];
async function worker() {
  while (cursor < assets.length) {
    const start = cursor; cursor += 256;
    const prefix = resolve(output, String(start).padStart(5, '0'));
    if (existsSync(prefix + '.checkpoint.json')) {
      const old = JSON.parse(readFileSync(prefix + '.checkpoint.json', 'utf8'));
      if (old.fingerprint === fingerprint && old.exitCode === 0) { records.push(old); continue; }
    }
    console.log(`Checking assets ${start}..${Math.min(start + 256, assets.length)}`);
    const log = [];
    const child = spawn('pnpm', ['exec', 'vitest', 'run', 'tests/simulatorLibrary.test.ts', '--reporter=json', '--outputFile=' + prefix + '.json'], {
      cwd: frontend,
      env: { ...process.env, HANDBOOK_KIT_DIR: prefix + '.kit', SIMULATOR_LIBRARY_DIR: resolve(library), SIMULATOR_ASSET_START: String(start), SIMULATOR_ASSET_LIMIT: '256' },
      stdio: ['ignore', 'pipe', 'pipe']
    });
    child.stdout.on('data', d => log.push(d)); child.stderr.on('data', d => log.push(d));
    const exitCode = await new Promise((done, reject) => { child.on('error', reject); child.on('close', done); });
    writeFileSync(prefix + '.log', Buffer.concat(log));
    const record = { fingerprint, start, count: Math.min(256, assets.length - start), exitCode };
    records.push(record); writeFileSync(prefix + '.checkpoint.json', JSON.stringify(record));
  }
}
await Promise.all([worker(), worker()]);
records.sort((a, b) => a.start - b.start);
const summary = { fingerprint, assets: assets.length, batches: records.length, failedBatches: records.filter(r => r.exitCode !== 0), records };
writeFileSync(resolve(output, 'summary.json'), JSON.stringify(summary, null, 2));
console.log(JSON.stringify({ assets: summary.assets, batches: summary.batches, failedBatches: summary.failedBatches.length }));
if (summary.failedBatches.length) process.exitCode = 1;
