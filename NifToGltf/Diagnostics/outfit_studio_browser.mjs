// Requires an already-authorized, running Handbook server. Does not start servers.
import { createRequire } from 'node:module';
import { mkdirSync, writeFileSync } from 'node:fs';
const frontend = process.env.HANDBOOK_FRONTEND ?? '/home/ubuntu/repos/MapleStory2-Handbook';
const { chromium, expect: baseExpect } = createRequire(frontend + '/package.json')('@playwright/test');
const expect = baseExpect.configure({ timeout: 60000 });
const evidence = process.env.OUTFIT_STUDIO_EVIDENCE ?? 'NifToGltf/obj/outfit-studio-20260908';
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
  await page.goto((process.env.OUTFIT_STUDIO_URL ?? 'http://127.0.0.1:4000') + '/outfits', { waitUntil: 'domcontentloaded' });
  await ready();
  await check('14 clickable slots', await page.locator('.equipment-slot, .appearance-slot').count() === 14);
  await check('combined equipment and outfits, without wardrobe type tabs', await page.getByRole('tab').count() === 0 && (await page.locator('.catalog-count').innerText()).includes('outfits & equipment'));
  await equip(10200010, 'Hair');
  await check('Sassy accessible without a query and starts at reviewed rear placements', await scene().evaluate((el) => el.outfitViewer.hairPlacementControls.every((c) => c.value === 2)));
  const placements = page.locator('.customize select').filter({ has: page.locator('option[value="2"]') }).filter({ has: page.locator('option', { hasText: 'Position 3' }) });
  await placements.nth(0).selectOption('0'); await placements.nth(1).selectOption('2');
  await page.getByLabel('Sassy Pigtails Primary', { exact: true }).fill('#d978b2');
  await page.getByLabel('Sassy Pigtails Primary', { exact: true }).dispatchEvent('change');
  await page.getByLabel('Browser hair motion', { exact: true }).check();
  const sample = () => scene().evaluate((el) => {
    const viewer = el.outfitViewer, rotations = [];
    viewer.scene.traverse((n) => { if (n.userData.equipmentBone && /Bone0[23]/.test(n.name)) rotations.push(...n.quaternion.toArray()); });
    return { active: viewer.inspect().browserHair.active, rotations, playing: viewer.playing };
  });
  const a = await sample(); await page.waitForTimeout(1200); const b = await sample();
  await check('continuous browser motion advances through actual animation frames', a.active && b.active && JSON.stringify(a.rotations) !== JSON.stringify(b.rotations));
  await page.getByRole('button', { name: 'Pause', exact: true }).click();
  const paused = await sample(); await page.waitForTimeout(600);
  await check('pause freezes the hair simulation', JSON.stringify(paused.rotations) === JSON.stringify((await sample()).rotations));
  await capture('sassy');
  const savedHair = await scene().evaluate((el) => ({ hair: el.outfitViewer.savedHairState, colors: el.outfitViewer.itemColorControls('10200010').map((c) => c.colors) }));
  for (const [id, form] of [[11300002, 'c'], [11300003, 'd']]) {
    await equip(id, 'Hats');
    await check(`Sassy ${form} hat form preserves independent controls and dye`, await scene().evaluate((el, expected) => {
      const v = el.outfitViewer;
      return v.equippedItems.find((b) => b.item.id === 10200010).hairForm === expected.form && JSON.stringify({ hair: v.savedHairState, colors: v.itemColorControls('10200010').map((c) => c.colors) }) === JSON.stringify(expected.savedHair);
    }, { form, savedHair }));
  }
  await page.getByRole('button', { name: /^Remove / }).click(); await ready();
  await equip(12220364, 'Tops');
  await check('full outfit occupies top and pants', (await inspect()).equipped.find((b) => b.id === 12220364).slots.join(',') === 'CL,PA');
  await check('both slots identify the full outfit', await page.locator('.equipment-slot .linked').count() === 2);
  await page.getByRole('button', { name: 'Import / export', exact: true }).click();
  const code = await exportCurrent();
  const before = parse(code);
  await page.locator('.studio-settings summary').click();
  await page.getByRole('button', { name: 'Clear outfit', exact: true }).click(); await ready();
  await check('clear removes all equipment', (await inspect()).equipped.length === 0);
  await page.getByLabel('Outfit code', { exact: true }).fill(code);
  await page.getByRole('button', { name: 'Import outfit', exact: true }).click(); await ready();
  await expect(page.getByText('Outfit imported. All items and saved appearance settings restored.', { exact: true })).toBeVisible();
  await check('export/import round trip restores exact logical appearance', JSON.stringify(parse(await exportCurrent())) === JSON.stringify(before));
  await capture('imported');
  for (const [name, bad] of [
    ['malformed', '<script>alert(1)</script>'], ['truncated', code.slice(0, -7)], ['oversized', 'MS2O.' + 'A'.repeat(48001)],
    ['missing item', pack({ ...before, items: [{ id: 2147483647, colors: [], animations: [] }] })],
    ['unavailable hair', pack({ ...before, items: [{ id: 10200031, colors: [], animations: [] }] })],
    ['conflicting slots', pack({ ...before, items: [...before.items, before.items[0]] })],
    ['staged color mismatch', pack({ ...before, bodyColors: [] })]
  ]) {
    await page.getByLabel('Outfit code', { exact: true }).fill(bad);
    await page.getByRole('button', { name: 'Import outfit', exact: true }).click(); await ready();
    await expect(page.locator('.sharing [role="alert"]')).toBeVisible();
    await check(`${name} import preserves current outfit`, JSON.stringify(parse(await exportCurrent())) === JSON.stringify(before));
  }
  await page.getByRole('button', { name: 'Close sharing', exact: true }).click();
  for (const id of [10200011, 10200012, 10200070]) {
    await equip(id, 'Hair'); await capture('hair-' + id);
    await check(`hair ${id} remains static without applying Sassy solver`, !(await inspect()).browserHair.active);
    for (const hat of [11300002, 11300003]) await equip(hat, 'Hats');
    await page.getByRole('button', { name: /^Remove / }).click(); await ready();
  }
  await equip(11500004, 'Pants');
  await check('separate pants evict the complete outfit', !(await inspect()).equipped.some((b) => b.id === 12220364));
  await slot('Hair'); await page.getByRole('searchbox').fill('');
  await page.screenshot({ path: evidence + '/desktop.png', fullPage: true });
  const hairSlot = page.getByRole('button', { name: /^Hair:/ }); await hairSlot.focus(); await page.keyboard.press('Enter');
  await check('keyboard activates slot', await hairSlot.getAttribute('aria-pressed') === 'true');
  await page.keyboard.press('Tab');
  await check('visible keyboard focus', await page.evaluate(() => getComputedStyle(document.activeElement).outlineStyle !== 'none'));
  await page.setViewportSize({ width: 390, height: 844 });
  await page.screenshot({ path: evidence + '/mobile.png', fullPage: true });
  await check('mobile has no horizontal overflow', await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
  await slot('Shoes');
  await check('mobile slot opens relevant panel', await page.locator('.item-panel h2').innerText() === 'Shoes');
  await page.getByRole('searchbox').scrollIntoViewIfNeeded(); await page.getByRole('searchbox').fill('11700004');
  await expect(page.getByRole('button', { name: /^Equip / }).first()).toBeEnabled();
  await page.getByRole('button', { name: /^Equip / }).first().click(); await ready();
  await check('mobile item equip works', (await inspect()).equipped.some((b) => b.id === 11700004));
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
