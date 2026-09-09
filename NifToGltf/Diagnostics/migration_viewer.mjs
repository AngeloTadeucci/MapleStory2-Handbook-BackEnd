// Exercise the actual standalone renderer in Chromium without starting a server.
// Network interception serves only the explicitly selected local fixture tree.
import { createRequire } from 'node:module';
import { readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { resolve, extname, sep } from 'node:path';
import assert from 'node:assert/strict';
const [frontend, fixtures, output] = process.argv.slice(2).map((path) => resolve(path));
const require = createRequire(resolve(frontend, 'package.json'));
const { chromium } = require('@playwright/test');
const { build } = createRequire(require.resolve('vite'))('esbuild');
const bundle = await build({
  absWorkingDir: frontend, entryPoints: ['src/lib/models/StandaloneScene.ts'],
  bundle: true, format: 'iife', globalName: 'MigrationViewer', write: false,
  tsconfig: resolve(frontend, 'tsconfig.json'),
  plugins: [{ name: 'test-environment', setup(builder) {
    builder.onResolve({ filter: /^\$(app\/environment|env\/static\/public)$/ }, (args) => ({ path: args.path, namespace: 'test-env' }));
    builder.onLoad({ filter: /.*/, namespace: 'test-env' }, () => ({ contents: "export const dev = false; export const PUBLIC_NODE_ENV = 'production'; export const PUBLIC_MODELS_URL = 'http://migration.test/';" }));
  } }]
});
mkdirSync(output, { recursive: true });
const browser = await chromium.launch({ executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH, headless: true,
  args: ['--no-sandbox', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
try {
  const page = await browser.newPage({ viewport: { width: 800, height: 800 } });
  const errors = [], requests = [];
  page.on('pageerror', (error) => errors.push(error.message));
  page.on('console', (message) => { if (message.type() === 'error') errors.push(message.text()); });
  await page.route('http://migration.test/**', async (route) => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/') {
      return route.fulfill({ contentType: 'text/html', body: '<main aria-label="Model test" style="width:760px;height:760px;background:#35171f"></main>' });
    }
    const file = resolve(fixtures, '.' + decodeURIComponent(path));
    if (!file.startsWith(fixtures + sep)) return route.abort();
    requests.push(path);
    try {
      await route.fulfill({ contentType: extname(file) === '.gltf' ? 'model/gltf+json' : 'application/octet-stream', body: readFileSync(file) });
    } catch { await route.fulfill({ status: 404, body: 'Missing fixture' }); }
  });
  await page.goto('http://migration.test/');
  await page.addScriptTag({ content: bundle.outputFiles[0].text });
  const assets = JSON.parse(readFileSync(resolve(fixtures, 'native-manifest.json'), 'utf8')).assets;
  const selected = process.env.MIGRATION_VIEWER_IDS?.split(',') ?? ['f_body', 'm_body', '10200001-male-0', 'wardrobe-ac9b6754d1a4d9b13f35f44c', 'wardrobe-cb58b9eba61b0a7124a8a236', '21000174_m_rabbitdollcymbalsgrey'];
  const results = [];
  for (const id of selected) {
    const asset = assets.find((asset) => asset.id === id);
    assert(asset, 'Missing selected model ' + id);
    const before = requests.filter((path) => path.endsWith('.gltf')).length;
    const result = await page.evaluate(async (asset) => {
      window.modelScene?.dispose();
      const scene = new MigrationViewer.StandaloneScene(document.querySelector('main'));
      window.modelScene = scene;
      await scene.load('http://migration.test/' + asset.uri, asset.facePreset ? {
        preset: asset.facePreset, customizationUrl: 'http://migration.test/' + asset.customizationUri
      } : undefined);
      scene.pause();
      scene.currentTime = 0;
      const names = scene.availableAnimations;
      if (names.length > 1) {
        scene.animationName = names[1];
        scene.currentTime = Math.min(0.25, scene.duration / 2);
        scene.timeScale = 2;
        const original = scene.currentTime;
        const source = scene.captureSource();
        const restore = source.begin();
        try { await source.frame(0); await source.frame(scene.duration / 2); }
        finally { restore(); }
        if (Math.abs(scene.currentTime - original) > 1e-6 || scene.timeScale !== 2)
          throw new Error('Capture did not restore playback');
        scene.currentTime = 0;
      }
      return { clips: names.length, screenshotBytes: scene.toDataURL().length, duration: scene.duration };
    }, asset);
    const modelRequests = requests.filter((path) => path.endsWith('.gltf')).length - before;
    assert.equal(modelRequests, 1, 'Clip switching or capture fetched another model');
    assert(result.screenshotBytes > 5000, 'Empty model screenshot');
    await page.screenshot({ path: resolve(output, id + '.png') });
    results.push({ id, ...result, modelRequests });
  }
  assert.deepEqual(errors, [], 'Unexpected browser errors');
  writeFileSync(resolve(output, 'results.json'), JSON.stringify({ results, errors }, null, 2));
  console.log(JSON.stringify({ models: results.length, errors: errors.length, results }));
} finally { await browser.close(); }
