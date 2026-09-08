// Requires an already-authorized, running Handbook server. Does not start servers.
import { createRequire } from 'node:module';
import { mkdirSync, writeFileSync } from 'node:fs';
const frontend = process.env.HANDBOOK_FRONTEND ?? '/home/ubuntu/repos/MapleStory2-Handbook';
const { chromium, expect: baseExpect } = createRequire(frontend + '/package.json')('@playwright/test');
const expect = baseExpect.configure({ timeout: 60000 });
const evidence = process.env.OUTFIT_STUDIO_EVIDENCE ?? 'NifToGltf/obj/outfit-studio-20260908/extended';
mkdirSync(evidence, { recursive: true });
const browser = await chromium.launch({ headless: true, executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
const page = await browser.newPage({ viewport: { width: 1440, height: 1050 } });
page.setDefaultTimeout(60000);
const checks = [], errors = [];
page.on('pageerror', (e) => errors.push(e.message));
await page.route('**/*', (r) => ['GET', 'HEAD'].includes(r.request().method()) ? r.continue() : r.abort());
const scene = () => page.locator('[aria-label="Outfit preview"]');
const inspect = () => scene().evaluate((el) => el.outfitViewer.inspect());
async function check(name, test) { expect(test, name).toBeTruthy(); checks.push(name); console.log('PASS', name); }
async function ready() { await expect(page.getByRole('button', { name: 'Save image', exact: true })).toBeEnabled(); }
async function slot(name) { await page.getByRole('button', { name: new RegExp('^' + name + ':') }).click(); }
async function equip(id, name) {
  await slot(name); await page.getByRole('searchbox').fill(String(id));
  const choice = page.getByRole('button', { name: /^Equip / }).first();
  await expect(choice).toBeEnabled(); await choice.click(); await ready();
  await expect.poll(async () => (await inspect()).equipped.some((b) => b.id === id)).toBe(true);
  const details = page.locator('.customize');
  if (await details.count() && !await details.getAttribute('open').then((v) => v !== null)) await details.locator('summary').first().click();
}
async function capture(name) {
  for (const angle of ['Front', 'Side', 'Back']) {
    await page.getByRole('button', { name: angle, exact: true }).click();
    await scene().evaluate((el) => el.outfitViewer.screenshot());
    await scene().screenshot({ path: `${evidence}/${name}-${angle.toLowerCase()}.png` });
  }
}
const parse = (text) => JSON.parse(Buffer.from(text.slice(5), 'base64url').toString('utf8'));
const pack = (data) => 'MS2O.' + Buffer.from(JSON.stringify(data)).toString('base64url');
async function exportCurrent() {
  await page.getByRole('button', { name: 'Export current outfit', exact: true }).click();
  return page.getByLabel('Outfit code', { exact: true }).inputValue();
}
async function importText(code) {
  await page.getByLabel('Outfit code', { exact: true }).fill(code);
  await page.getByRole('button', { name: 'Import outfit', exact: true }).click(); await ready();
  await expect(page.getByText('Outfit imported. All items and saved appearance settings restored.', { exact: true })).toBeVisible();
}
async function selectedColor(label, value) {
  const input = page.getByLabel(label, { exact: true });
  await input.fill(value); await input.dispatchEvent('change');
}
async function slider(label, value) {
  const input = page.locator('label').filter({ hasText: label }).locator('input[type="range"]').first();
  await input.evaluate((el, value) => { el.value = String(value); el.dispatchEvent(new Event('input', { bubbles: true })); }, value);
}
try {
  await page.goto((process.env.OUTFIT_STUDIO_URL ?? 'http://127.0.0.1:4000') + '/outfits', { waitUntil: 'domcontentloaded' }); await ready();
  if (!process.env.OUTFIT_STUDIO_SKIP_TAILS) {
  await equip(10200010, 'Hair');
  await page.getByRole('button', { name: 'Import / export', exact: true }).click();
  const independent = parse(await exportCurrent());
  independent.items[0].hair.tails = [{ position: 2, scale: 0.8 }, { position: 0, scale: 1.2 }];
  independent.browserMotion = true;
  await importText(pack(independent));
  await check('independent tail sizes import exactly', JSON.stringify((parse(await exportCurrent())).items[0].hair.tails) === JSON.stringify(independent.items[0].hair.tails));
  await page.getByRole('button', { name: 'Close sharing', exact: true }).click();
  for (const hat of [11300002, 11300003]) {
    await equip(hat, 'Hats');
    await check(`independent tail sizes survive hat ${hat}`, await scene().evaluate((el, tails) => JSON.stringify(el.outfitViewer.savedHairState.tails) === JSON.stringify(tails), independent.items[0].hair.tails));
  }
  await page.getByRole('button', { name: /^Remove / }).click(); await ready();
  await page.getByRole('button', { name: 'Import / export', exact: true }).click();
  const before = await exportCurrent();
  const invalid = parse(before); invalid.items[0].hair.tails[0].position = 15;
  await page.getByLabel('Outfit code', { exact: true }).fill(pack(invalid));
  await page.getByRole('button', { name: 'Import outfit', exact: true }).click(); await ready();
  await expect(page.locator('.sharing [role="alert"]')).toContainText('Unknown hair placement');
  await check('invalid actual source position preserves the original scene', await exportCurrent() === before);
  const badAsset = '**/sassy-pigtails-preview-01/wardrobe/sassy-pigtails-tail-1.gltf';
  await page.route(badAsset, (r) => r.fulfill({ status: 404, body: 'QA missing model' }));
  await page.getByLabel('Outfit code', { exact: true }).fill(before);
  await page.getByRole('button', { name: 'Import outfit', exact: true }).click(); await ready();
  await expect(page.locator('.sharing [role="alert"]')).toBeVisible();
  await check('model fetch failure preserves the original scene', await exportCurrent() === before);
  await page.unroute(badAsset);
  await page.getByRole('button', { name: 'Close sharing', exact: true }).click();
  }
  await equip(10200213, 'Hair');
  const hairName = await page.locator('.selected-title span').innerText();
  await slider(hairName + ' length 1', 0.63); await slider(hairName + ' length 2', 0.37);
  const palette = page.getByLabel(hairName + ' palette', { exact: true });
  const choice = await palette.locator('option').nth(3).getAttribute('value'); await palette.selectOption(choice);
  await check('both hair length channels and palette reach the scene', await scene().evaluate((el) => {
    const v = el.outfitViewer; return Math.abs(v.savedHairState.lengths[0] - 0.63) < 1e-8 && Math.abs(v.savedHairState.lengths[1] - 0.37) < 1e-8;
  }));
  await equip(12220364, 'Tops');
  const dressName = await page.locator('.selected-title span').innerText();
  await selectedColor(dressName + ' Primary', '#58a8ce');
  await equip(10400011, 'Makeup');
  await page.getByLabel('Makeup placement', { exact: true }).selectOption('1');
  await slider('Makeup size', 0.14);
  await page.locator('.studio-settings summary').click();
  await page.getByLabel('Expression', { exact: true }).selectOption('happy');
  await page.getByLabel('Background', { exact: true }).selectOption('henesys_a');
  await expect.poll(async () => scene().evaluate((el) => el.outfitViewer.scene.background?.isTexture === true)).toBe(true);
  await selectedColor('Skin Primary', '#c38963');
  await selectedColor('Eyes Primary', '#4488bb');
  await equip(13400306, 'Left hand');
  await equip(13400306, 'Right hand');
  await page.getByRole('button', { name: 'Stow weapons', exact: true }).click(); await ready();
  await check('two copies preserve explicit hand identities when stowed', await scene().evaluate((el) => {
    const w = el.outfitViewer.equippedItems.filter((b) => b.item.id === 13400306);
    return w.length === 2 && w.every((b) => b.weaponPlacement === 'stowed') && new Set(w.map((b) => b.hand)).size === 2;
  }));
  await page.getByRole('button', { name: 'Import / export', exact: true }).click();
  const settingsCode = await exportCurrent();
  await page.getByRole('button', { name: 'Clear outfit', exact: true }).click(); await ready();
  await importText(settingsCode);
  const after = await exportCurrent();
  await check('full settings round trip preserves makeup, background, expression, hair lengths, dyes and hands', JSON.stringify(parse(after)) === JSON.stringify(parse(settingsCode)));
  await page.getByRole('button', { name: 'Close sharing', exact: true }).click();
  await page.getByRole('button', { name: 'Pause', exact: true }).click();
  await capture('customized');
  const downloadEvent = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Save image', exact: true }).click();
  const download = await downloadEvent; await download.saveAs(evidence + '/exported-image.png');
  await check('image export downloads a PNG', download.suggestedFilename() === 'maplestory2-outfit.png');
  await page.getByLabel('Body', { exact: true }).selectOption('male'); await ready();
  await equip(10200121, 'Hair'); await equip(11400367, 'Tops'); await equip(11500004, 'Pants');
  await page.getByRole('button', { name: 'Import / export', exact: true }).click();
  const maleCode = await exportCurrent();
  await page.getByLabel('Body', { exact: true }).selectOption('female'); await ready(); await importText(maleCode);
  await check('import replaces the body along with compatible items', (await inspect()).body === 'male' && await exportCurrent() === maleCode);
  await page.getByRole('button', { name: 'Close sharing', exact: true }).click();
  await page.getByRole('button', { name: 'Pause', exact: true }).click(); await capture('male');
  await page.getByRole('button', { name: 'Side', exact: true }).click();
  const direction = await scene().evaluate((el) => {
    const v = el.outfitViewer; return v.camera.position.clone().sub(v.controls.target).normalize().toArray();
  });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.waitForTimeout(300);
  await check('mobile resize preserves the current viewing direction', await scene().evaluate((el, expected) => {
    const v = el.outfitViewer, actual = v.camera.position.clone().sub(v.controls.target).normalize().toArray();
    return actual.every((n, i) => Math.abs(n - expected[i]) < 1e-6);
  }, direction));
  const bounds = await scene().evaluate((el) => {
    const v = el.outfitViewer; const box = v.outfitBounds(); let maxX = 0, maxY = 0;
    v.camera.updateMatrixWorld();
    for (const x of [box.min.x, box.max.x]) for (const y of [box.min.y, box.max.y]) for (const z of [box.min.z, box.max.z]) {
      const point = v.camera.position.clone().set(x, y, z).project(v.camera); maxX = Math.max(maxX, Math.abs(point.x)); maxY = Math.max(maxY, Math.abs(point.y));
    }
    return { maxX, maxY };
  });
  await check('mobile character bounds fit inside the canvas after resize', bounds.maxX < 1 && bounds.maxY < 1);
  await page.locator('.fitting-room').scrollIntoViewIfNeeded(); await page.screenshot({ path: evidence + '/mobile-character.png' });
  await slot('Hair'); await page.getByRole('searchbox').fill('');
  await expect.poll(async () => page.locator('.item-card').count()).toBeGreaterThan(1);
  await page.locator('.item-panel').scrollIntoViewIfNeeded(); await page.screenshot({ path: evidence + '/mobile-picker.png' });
  await check('mobile page fits width including picker', await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
  await page.setViewportSize({ width: 1440, height: 1050 });
  await page.locator('.page-header').scrollIntoViewIfNeeded(); await page.screenshot({ path: evidence + '/desktop.png' });
  await check('no browser script errors', errors.length === 0);
} catch (error) {
  errors.push(error.stack ?? String(error));
  await page.screenshot({ path: evidence + '/failure.png', fullPage: true }).catch(() => {});
  writeFileSync(evidence + '/failure-dom.txt', await page.locator('body').innerText().catch(() => ''));
  process.exitCode = 1;
} finally {
  writeFileSync(evidence + '/report.json', JSON.stringify({ checks, errors }, null, 2));
  console.log(JSON.stringify({ checks: checks.length, errors }, null, 2));
  await browser.close();
}
