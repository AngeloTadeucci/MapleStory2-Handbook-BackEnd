// Requires an already-authorized, running Handbook server. Does not start servers.
import { createRequire } from 'node:module';
import { mkdirSync, writeFileSync } from 'node:fs';
const frontend = process.env.HANDBOOK_FRONTEND ?? '/home/ubuntu/repos/MapleStory2-Handbook';
const { chromium, expect: baseExpect } = createRequire(frontend + '/package.json')('@playwright/test');
const expect = baseExpect.configure({ timeout: 60000 });
const evidence = process.env.OUTFIT_STUDIO_EVIDENCE ?? 'NifToGltf/obj/outfit-studio-20260908/final';
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
try {
  await page.goto((process.env.OUTFIT_STUDIO_URL ?? 'http://127.0.0.1:4000') + '/outfits', { waitUntil: 'domcontentloaded' }); await ready();
  await equip(10200070, 'Hair'); await slot('Hair');
  await check('clicking a slot starts with customization collapsed', await page.locator('.customize').getAttribute('open') === null);
  await expect.poll(async () => page.locator('.item-card').count()).toBeGreaterThan(1);
  await page.screenshot({ path: evidence + '/desktop-items.png' });
  await page.locator('.customize summary').click();
  const size = page.locator('label').filter({ hasText: 'Curly Ponytail tail size' }).locator('input[type="range"]');
  await size.evaluate((el) => { el.value = '1.1'; el.dispatchEvent(new Event('input', { bubbles: true })); });
  await check('expanded customization still changes the selected hair', await scene().evaluate((el) => el.outfitViewer.savedHairState.tails[0].scale === 1.1));
  if (!process.env.OUTFIT_STUDIO_CAMERA_ONLY) {
  await equip(10400011, 'Makeup');
  await page.getByLabel('Makeup placement', { exact: true }).selectOption('1');
  await page.getByRole('button', { name: 'Import / export', exact: true }).click();
  const code = await exportCurrent();
  await page.context().grantPermissions(['clipboard-read', 'clipboard-write']);
  await page.getByRole('button', { name: 'Copy code', exact: true }).click();
  await check('copy writes the outfit code to the clipboard', await page.evaluate(() => navigator.clipboard.readText()) === code);
  const downloadEvent = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Save text', exact: true }).click();
  const download = await downloadEvent; await download.saveAs(evidence + '/outfit-code.txt');
  await check('text export downloads a file', download.suggestedFilename() === 'maplestory2-outfit.txt');
  await page.locator('.studio-settings summary').click();
  await page.getByRole('button', { name: 'Clear outfit', exact: true }).click(); await ready();
  await page.getByRole('button', { name: 'Import outfit', exact: true }).click(); await ready();
  await expect(page.getByText('Outfit imported. All items and saved appearance settings restored.', { exact: true })).toBeVisible();
  await check('latest UI still round trips appearance', await exportCurrent() === code);
  await page.getByRole('button', { name: 'Close sharing', exact: true }).click();
  await slot('Makeup'); await page.locator('.customize summary').click();
  await check('makeup dropdown displays the imported source placement', await page.getByLabel('Makeup placement', { exact: true }).inputValue() === '1');
  }
  if (await page.locator('.studio-settings').getAttribute('open') === null) await page.locator('.studio-settings summary').click();
  await page.getByLabel('Background', { exact: true }).selectOption('henesys_a');
  await expect.poll(async () => scene().evaluate((el) => el.outfitViewer.scene.background?.isTexture === true)).toBe(true);
  await page.locator('.fitting-room').scrollIntoViewIfNeeded();
  const camera = () => scene().evaluate((el) => {
    const v = el.outfitViewer; return { position: v.camera.position.toArray(), distance: v.camera.position.distanceTo(v.controls.target) };
  });
  const start = await camera(), box = await scene().boundingBox();
  await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
  await page.mouse.down(); await page.mouse.move(box.x + box.width / 2 + 90, box.y + box.height / 2 + 25, { steps: 8 }); await page.mouse.up();
  await page.waitForTimeout(300);
  await check('dragging the preview rotates the camera', JSON.stringify((await camera()).position) !== JSON.stringify(start.position));
  const distance = (await camera()).distance; await page.mouse.wheel(0, -150); await page.waitForTimeout(300);
  await check('scrolling the preview zooms the camera', Math.abs((await camera()).distance - distance) > 0.001);
  await page.getByRole('button', { name: 'Front', exact: true }).click();
  await page.getByRole('button', { name: 'Pause', exact: true }).click();
  await page.setViewportSize({ width: 390, height: 844 }); await page.waitForTimeout(500);
  await page.locator('.fitting-room').scrollIntoViewIfNeeded();
  await check('portrait background crops instead of stretching', await scene().evaluate((el) => {
    const v = el.outfitViewer, t = v.scene.background;
    return t.repeat.x < 1 && t.repeat.y === 1 && Math.abs(t.image.naturalWidth / t.image.naturalHeight * t.repeat.x / t.repeat.y - v.camera.aspect) < 1e-6;
  }));
  await page.screenshot({ path: evidence + '/mobile-character.png' });
  await slot('Hair');
  await expect.poll(async () => page.locator('.item-card').count()).toBeGreaterThan(1);
  await page.locator('.item-panel').scrollIntoViewIfNeeded(); await page.screenshot({ path: evidence + '/mobile-items.png' });
  await check('mobile item panel shows choices with customization collapsed', await page.locator('.customize').getAttribute('open') === null && await page.locator('.item-card').count() > 1);
  await check('mobile page has no horizontal overflow', await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
  await page.setViewportSize({ width: 1440, height: 1050 });
  await page.locator('.page-header').scrollIntoViewIfNeeded(); await page.screenshot({ path: evidence + '/desktop-final.png' });
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
