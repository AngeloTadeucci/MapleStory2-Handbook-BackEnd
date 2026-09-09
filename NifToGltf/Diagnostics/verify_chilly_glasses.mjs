// Render Chilly glasses with the actual simulator scene, without a server.
import { createRequire } from 'node:module';
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { resolve, sep } from 'node:path';
import assert from 'node:assert/strict';
const [frontend, release, batch, entriesFile, output] = process.argv.slice(2).map(path => resolve(path));
const require = createRequire(resolve(frontend, 'package.json'));
const { chromium } = require('@playwright/test');
const { build } = createRequire(require.resolve('vite'))('esbuild');
const bundle = await build({
  absWorkingDir: frontend, entryPoints: ['src/lib/outfits/OutfitScene.ts'],
  bundle: true, format: 'iife', globalName: 'RetryScene', write: false,
  plugins: [{ name: 'local-fixture', setup(builder) {
    builder.onResolve({ filter: /^\$(app\/environment|env\/static\/public)$/ }, args => ({ path: args.path, namespace: 'fixture' }));
    builder.onLoad({ filter: /.*/, namespace: 'fixture' }, () => ({ contents:
      "export const dev=false; export const PUBLIC_NODE_ENV='production'; export const PUBLIC_MODELS_URL='http://retry.test/';" }));
  } }]
});
const read = path => JSON.parse(readFileSync(path, 'utf8'));
const bodies = read(resolve(release, 'native-manifest.json')).assets;
const assets = read(resolve(batch, 'native-manifest.json')).assets;
const entries = read(entriesFile);
mkdirSync(output, { recursive: true });
const browser = await chromium.launch({ executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH,
  args: ['--no-sandbox', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
const results = [], errors = [], requests = [];
try {
  const page = await browser.newPage({ viewport: { width: 900, height: 900 } });
  page.on('pageerror', error => errors.push(error.message));
  page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });
  await page.route('http://retry.test/**', async route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/') return route.fulfill({ contentType: 'text/html', body:
      '<main style="width:850px;height:850px;background:#ddd"></main>' });
    const root = path.startsWith('/body/') ? release : batch;
    const file = resolve(root, decodeURIComponent(path.replace(/^\/(body|gear)\//, '')));
    if (!file.startsWith(root + sep)) return route.abort();
    requests.push(path);
    try { return route.fulfill({ contentType: 'model/gltf+json', body: readFileSync(file) }); }
    catch (error) { errors.push(error.message); return route.fulfill({ status: 404, body: 'Missing fixture' }); }
  });
  await page.goto('http://retry.test/');
  await page.addScriptTag({ content: bundle.outputFiles[0].text });
  for (const body of ['female']) {
    const asset = assets.find(asset => asset.bodyVariant === body && asset.input.includes('11150058_f_eyvalentine03'));
    const entry = entries.find(entry => entry.bodyVariant === body);
    const bodyAsset = bodies.find(asset => asset.id === (body === 'male' ? 'm_body' : 'f_body'));
    assert(asset && entry && bodyAsset, 'Missing body or retry asset');
    const before = requests.length;
    const result = await page.evaluate(async ({ asset, entry, bodyAsset }) => {
      window.retryScene?.destroy();
      const scene = new RetryScene.OutfitScene(document.querySelector('main'));
      window.retryScene = scene;
      const clips = await scene.setBody({ ...bodyAsset, url: 'http://retry.test/body/' + bodyAsset.uri });
      await scene.equipBundle({ item: { id: entry.itemId, name: entry.sourceName, library: entry },
        parts: [{ ...asset, url: 'http://retry.test/gear/' + asset.uri }], slots: entry.slots });
      scene.view('front'); scene.seek(0);
      const original = scene.screenshot();
      const colorControls = scene.itemColorControls(String(entry.itemId));
      if (!colorControls.length) throw Error('No glasses dye controls');
      scene.setItemColors(entry.itemId, [[0.07,0.2,0.34],[0.7,0.4,0.2],[0.2,0.7,0.86]]);
      scene.seek(0);
      const dyed = scene.screenshot();
      if (original === dyed) throw Error('Glasses dye did not change pixels');
      scene.selectClip(clips.includes('run_a') ? 'run_a' : clips[0]); scene.seek(0.3);
      const running = scene.screenshot();
      if (running === dyed) throw Error('Body motion did not change pixels');
      scene.selectClip(clips[0]); scene.seek(0);
      const lit = scene.screenshot();
      const uniforms = [];
      scene.scene.traverse(node => {
        for (const material of (Array.isArray(node.material) ? node.material : [node.material])) {
          if (!material?.userData?.nifLighting?.sceneAmbient) continue;
          const prior = material.onBeforeCompile;
          material.onBeforeCompile = function(shader, renderer) {
            prior.call(this, shader, renderer);
            uniforms.push(shader.uniforms.ms2SceneAmbient);
          };
          const oldKey = material.customProgramCacheKey();
          material.customProgramCacheKey = () => oldKey + ':ambient-verification';
          material.needsUpdate = true;
        }
      });
      scene.seek(0);
      if (uniforms.length !== 1) throw Error('Expected exactly one glasses ambient material');
      uniforms[0].value.set(0,0,0); scene.seek(0);
      if (scene.screenshot() === lit) throw Error('Authored ambient did not change pixels');
      uniforms[0].value.set(1,1,1); scene.seek(0);
      if (scene.screenshot() !== lit) throw Error('Restoring authored ambient changed the pose');
      return { state: scene.inspect(), dyeChangesPixels: true, bodyMotion: true, ambientChangesPixels: true, ambientRestoresPixels: true };
    }, { asset, entry, bodyAsset });
    assert.equal(requests.length - before, 2, 'Clip selection or dye reloaded a model');
    await page.screenshot({ path: resolve(output, body + '-moving-dyed.png') });
    for (const view of ['front', 'back', 'side']) {
      await page.evaluate(view => { window.retryScene.view(view); window.retryScene.seek(0.45); }, view);
      await page.screenshot({ path: resolve(output, body + '-' + view + '.png') });
    }
    results.push({ body, ...result, modelRequests: requests.length - before });
  }
  assert.deepEqual(errors, []);
} finally {
  await browser.close();
  writeFileSync(resolve(output, 'results.json'), JSON.stringify({ results, errors, requests }, null, 2));
}
console.log(JSON.stringify({ bodies: results.length, errors }));
