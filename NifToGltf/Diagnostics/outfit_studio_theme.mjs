// Checks the updated UI against an existing authorized review server.
import { createRequire } from 'node:module';
import { mkdirSync, writeFileSync } from 'node:fs';
const { chromium, expect: baseExpect } = createRequire('/home/ubuntu/repos/MapleStory2-Handbook/package.json')('@playwright/test');
const expect = baseExpect.configure({ timeout: 60000 });
const evidence = 'NifToGltf/obj/outfit-studio-20260908/theme';
mkdirSync(evidence, { recursive: true });
const browser = await chromium.launch({ executablePath: '/home/ubuntu/.cache/ms-playwright/chromium-1243/chrome-linux-arm64/chrome', headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
const page = await browser.newPage({ viewport: { width: 1440, height: 1050 } });
page.setDefaultTimeout(60000);
const errors = [], checks = [];
page.on('pageerror', error => errors.push(error.message));
await page.route('**/*', route => ['GET', 'HEAD'].includes(route.request().method()) ? route.continue() : route.abort());
const scene = () => page.locator('[aria-label="Outfit preview"]');
const ready = () => expect(page.getByRole('button', { name: 'Save image', exact: true })).toBeEnabled();
async function check(name, condition) { expect(condition, name).toBeTruthy(); checks.push(name); console.log('PASS', name); }
async function slot(name) { await page.getByRole('button', { name: new RegExp('^' + name + ':') }).click(); }
async function equip(id, name) {
  await slot(name); await page.getByRole('searchbox').fill(String(id));
  const button = page.getByRole('button', { name: /^Equip / }).first();
  await expect(button).toBeEnabled(); await button.click(); await ready();
  await expect(button).toHaveAttribute('aria-pressed', 'true');
}
try {
  await page.goto('http://100.118.72.53:4000/outfits', { waitUntil: 'domcontentloaded' }); await ready();
  await expect.poll(() => page.locator('.item-card').count()).toBeGreaterThan(0);
  await check('14 slots, without pendants, rings, belts or ears; earrings retained', await page.locator('.equipment-slot').count() === 14 && await page.getByRole('button', { name: /^(Pendants|Rings|Belts|Ears):/ }).count() === 0 && await page.getByRole('button', { name: /^Earrings:/ }).count() === 1);
  await check('subtitle, status badges, diagnostic details and availability filter removed', await page.getByText('Choose a slot. Find your style. Make it yours.', { exact: true }).count() === 0 && await page.getByText(/^(Why unavailable\?|Preview|Equipped|Verified)$/).count() === 0 && await page.locator('.availability-filter, .limitations, .panel-footnote').count() === 0);
  const palette = await page.locator('.character-panel').evaluate(el => ({ panel: getComputedStyle(el).backgroundColor, text: getComputedStyle(el).color, expectedText: getComputedStyle(el).getPropertyValue('--color-surface-50').trim() }));
  await check('panels use Handbook main-container gray and shared text color', palette.panel === 'rgb(43, 44, 46)' && palette.text === 'rgb(222, 222, 222)');
  await check('canvas uses Handbook gray2 background', await scene().evaluate(el => el.outfitViewer.scene.background.getHexString() === '37393d'));
  for (const body of ['female', 'male']) {
    for (const key of ['HR','CP','CL','PA','GL','RH','SH','FA','FD','EA','EY','FH','LH','MT']) {
      const response = await page.request.get(`http://100.118.72.53:4000/api/outfits?body=${body}&slot=${key}&availability=preview&outfit=all`);
      expect(response.ok()).toBeTruthy();
      const result = await response.json();
      expect(result.total).toBeGreaterThan(0);
      expect(result.items.every(item => item.library.availability !== 'unavailable')).toBeTruthy();
    }
  }
  await check('every retained slot has usable choices for both bodies', true);
  await equip(10200070, 'Hair'); await page.locator('.customize summary').click();
  const dye = page.locator('.customize input[type="color"]').first();
  await dye.fill('#d978b2'); await dye.dispatchEvent('change');
  await check('color controls retain shared theme and apply dye', await dye.inputValue() === '#d978b2' && await page.locator('.customize select').first().evaluate(el => getComputedStyle(el).backgroundColor === 'rgb(25, 26, 26)'));
  await equip(12220364, 'Tops');
  await check('full outfit still links both clothing slots without status badges', await page.getByRole('button', { name: /^Tops:/ }).innerText().then(t => t.includes('Full outfit')) && await page.getByRole('button', { name: /^Pants:/ }).innerText().then(t => t.includes('Full outfit')) && await page.getByText(/^(Preview|Equipped|Verified)$/).count() === 0);
  await page.getByRole('button', { name: 'Import / export', exact: true }).click();
  await page.getByRole('button', { name: 'Export current outfit', exact: true }).click();
  const code = await page.getByLabel('Outfit code', { exact: true }).inputValue();
  await page.locator('.studio-settings summary').click();
  await page.getByRole('button', { name: 'Clear outfit', exact: true }).click(); await ready();
  await page.getByRole('button', { name: 'Import outfit', exact: true }).click(); await ready();
  await expect(page.getByText('Outfit imported. All items and saved appearance settings restored.', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Export current outfit', exact: true }).click();
  await check('outfit and dyes round trip after clearing', await page.getByLabel('Outfit code', { exact: true }).inputValue() === code);
  await page.getByRole('button', { name: 'Close sharing', exact: true }).click();
  await page.locator('.studio-settings summary').click();
  await slot('Hair'); await expect.poll(() => page.locator('.item-card').count()).toBeGreaterThan(1);
  await page.getByRole('button', { name: 'Pause', exact: true }).click();
  await page.locator('.page-header').scrollIntoViewIfNeeded();
  await page.screenshot({ path: evidence + '/desktop.png' });
  const hair = page.getByRole('button', { name: /^Hair:/ }); await hair.focus(); await page.keyboard.press('Enter');
  await check('keyboard slot activation retains visible focus', await hair.evaluate(el => el.matches(':focus-visible')));
  await page.setViewportSize({ width: 390, height: 844 });
  await page.locator('.fitting-room').scrollIntoViewIfNeeded();
  await page.screenshot({ path: evidence + '/mobile-character.png' });
  await page.locator('.item-panel').scrollIntoViewIfNeeded();
  await page.screenshot({ path: evidence + '/mobile-items.png' });
  await check('mobile has no horizontal overflow', await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
  await slot('Hats'); await page.getByRole('searchbox').fill('11300002');
  const hat = page.getByRole('button', { name: /^Equip / }).first(); await expect(hat).toBeEnabled(); await hat.click(); await ready();
  await check('mobile slot picker equips an item', await scene().evaluate(el => el.outfitViewer.equippedItems.some(b => b.item.id === 11300002)));
  await check('no browser script errors', errors.length === 0);
} catch (error) { errors.push(error.stack ?? String(error)); process.exitCode = 1; await page.screenshot({ path: evidence + '/failure.png', fullPage: true }).catch(() => {}); }
finally { writeFileSync(evidence + '/report.json', JSON.stringify({ checks, errors }, null, 2)); console.log(JSON.stringify({ checks: checks.length, errors }, null, 2)); await browser.close(); }
