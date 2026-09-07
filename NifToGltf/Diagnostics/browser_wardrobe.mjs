// Source env.sh first. Capture the actual development route; never write to its API.
import { createRequire } from 'node:module';
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { resolve } from 'node:path';
const { chromium, expect } = createRequire(process.env.HANDBOOK_FRONTEND + '/package.json')('@playwright/test');
const [configuration, output] = process.argv.slice(2);
if (!configuration || !output) throw Error('Usage: node browser_wardrobe.mjs cases.json output-directory');
const cases = JSON.parse(readFileSync(configuration, 'utf8'));
mkdirSync(output, { recursive: true });
const browser = await chromium.launch({ headless: true, executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
page.setDefaultTimeout(60000);
const errors = [], results = [];
page.on('pageerror', error => errors.push(error.message));
await page.route('**/*', route => ['GET', 'HEAD'].includes(route.request().method()) ? route.continue() : route.abort());
try {
  await page.goto(process.env.WARDROBE_PREVIEW_URL ?? 'http://127.0.0.1:4002/outfits', { waitUntil: 'domcontentloaded' });
  await expect(page.getByRole('button', { name: 'Save image', exact: true })).toBeEnabled({ timeout: 60000 });
  const gpu = await page.evaluate(async () => {
    const m = await import('/src/routes/dev/nif-converter/characterShaderAcceptance.ts');
    return { shader: m.verifyCharacterShader(), pipeline: await m.verifyCharacterLightingPipeline() };
  });
  writeFileSync(resolve(output, 'shader-checks.json'), JSON.stringify(gpu, null, 2));
  for (const testCase of cases) {
    console.log(testCase.name, testCase.body, testCase.ids);
    const result = await page.evaluate(async c => {
      const { catalogSchema, resolveBundle, libraryBase } = await import('/src/lib/outfits/catalog.ts');
      const { joinCatalog } = await import('/src/lib/outfits/search.ts');
      const { parseNativeManifest } = await import('/src/lib/nativeAssets.ts');
      const url = new URL(libraryBase + 'native-manifest.json', location.href).href;
      const assets = parseNativeManifest(await (await fetch(url)).json(), url);
      const entries = catalogSchema.parse(await (await fetch(libraryBase + 'simulator-catalog.json')).json()).items;
      const items = joinCatalog(entries, []);
      const viewer = document.querySelector('[aria-label="Outfit preview"]').outfitViewer;
      await viewer.setBody(assets.find(a => a.id === c.body[0] + '_body'));
      for (const id of c.ids) {
        const item = items.find(i => i.id === id && i.library.bodyVariant === c.body);
        if (!item) throw Error(`Missing ${id}/${c.body}`);
        await viewer.equipBundle(resolveBundle(item, assets, c.body));
      }
      viewer.selectExpression(c.expression ?? 'default');
      viewer.selectClip(c.pose ?? 'fitting_idle_a');
      if (c.placement) await viewer.setWeaponPlacement(c.placement);
      viewer.seek(c.time ?? .65);
      const before = viewer.inspect();
      const makeup = viewer.makeupControls;
      if (c.makeup && makeup) { makeup.place(c.makeup.placement); makeup.scale(c.makeup.scale); }
      let dye;
      if (c.dye) {
        const control = viewer.colorControls.find(control => control.label.startsWith(c.dye.label));
        if (!control) throw Error('Dye control missing: ' + c.dye.label);
        const initial = structuredClone(control.colors);
        control.set(0, c.dye.color);
        const changed = structuredClone(control.colors);
        control.reset();
        if (JSON.stringify(control.colors) !== JSON.stringify(initial)) throw Error('Dye reset changed source defaults');
        dye = { initial, changed, reset: structuredClone(control.colors) };
      }
      if (c.hairLength) {
        if (!viewer.hairControls.length) throw Error('Expected source hair length controls');
        for (const control of viewer.hairControls) control.set(c.hairLength === 'min' ? control.values[0] : control.values.at(-1));
      }
      if (c.palette) {
        const bundle = viewer.equippedItems.find(b => b.item.id === c.palette.itemId);
        const customize = await (await fetch(libraryBase + 'customization.json')).json();
        const palette = customize.palettes[bundle.item.library.customize.colorPalette].find(p => p.id === c.palette.index);
        const control = viewer.colorControls.find(p => p.label === bundle.item.name);
        if (!palette || !control) throw Error('Source palette control missing');
        control.setColors(palette.colors);
      }
      if (c.sampleTime !== undefined) viewer.seek(c.sampleTime);
      return { before, after: viewer.inspect(), hairLengths: viewer.hairControls.map(c => ({label:c.label,value:c.value,values:c.values})), bundles: viewer.equippedItems.map(b => ({ itemId: b.item.id, hand: b.hand, hairForm: b.hairForm, weaponPlacement: b.weaponPlacement, parts: b.parts.map(a => ({ id: a.id, input: a.input, uri: a.uri, clips: a.clips })) })), dye, makeup: makeup ? { value: makeup.value, scaleRange: makeup.scaleRange } : null };
    }, testCase);
    const captures = [];
    for (const angle of testCase.angles ?? ['Front', 'Back']) {
      await page.getByRole('button', { name: angle, exact: true }).click();
      const file = `${testCase.name}-${testCase.body}-${angle.toLowerCase()}.png`;
      await page.locator('[aria-label="Outfit preview"]').screenshot({ path: resolve(output, file) });
      captures.push(file);
    }
    results.push({ ...testCase, ...result, captures });
    writeFileSync(resolve(output, 'results.json'), JSON.stringify({ results, errors }, null, 2));
  }
  if (errors.length) throw Error(errors.join('\n'));
} finally {
  writeFileSync(resolve(output, 'results.json'), JSON.stringify({ results, errors }, null, 2));
  await browser.close();
}
