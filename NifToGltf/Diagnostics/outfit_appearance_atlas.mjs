// Uses the existing authorized tailnet review server and local client artwork.
import { createRequire } from 'node:module';
import { mkdirSync, writeFileSync } from 'node:fs';
const { chromium, expect: baseExpect } = createRequire('/home/ubuntu/repos/MapleStory2-Handbook/package.json')('@playwright/test');
const expect = baseExpect.configure({ timeout: 60000 });
const evidence = 'NifToGltf/obj/outfit-studio-20260908/appearance-atlas';
mkdirSync(evidence, { recursive: true });
const browser = await chromium.launch({ executablePath: '/home/ubuntu/.cache/ms-playwright/chromium-1243/chrome-linux-arm64/chrome', headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
const page = await browser.newPage({ viewport: { width: 1440, height: 1100 } });
page.setDefaultTimeout(60000);
const checks = [], errors = [];
page.on('pageerror', error => errors.push(error.message));
await page.route('**/*', route => ['GET','HEAD'].includes(route.request().method()) ? route.continue() : route.abort());
const ready = () => expect(page.getByRole('button', { name: 'Save image', exact: true })).toBeEnabled();
const slot = name => page.getByRole('button', { name: new RegExp('^' + name + ':') });
async function check(name, condition) { expect(condition, name).toBeTruthy(); checks.push(name); console.log('PASS', name); }
async function equip(name, id) {
 await slot(name).click(); await page.getByRole('searchbox',{name:'Find an item',exact:true}).fill(String(id));
 const button = page.getByRole('button', { name: /^Equip / }).first();
 await expect(button).toBeEnabled(); await button.click(); await ready();
 await expect(button).toHaveAttribute('aria-pressed', 'true');
 await expect(slot(name)).toHaveAttribute('aria-pressed', 'true');
}
try {
 await page.goto('http://100.118.72.53:4000/outfits', { waitUntil: 'domcontentloaded' }); await ready();
 await check('appearance slots reuse the requested helmet, lollipop and glasses artwork', await page.locator('.appearance-controls').evaluate(el => {
   const icons = [...el.querySelectorAll('img')];
   return !el.querySelector('svg') && icons.length === 3 && icons.every((icon, index) => icon.getAttribute('src') === ['/outfits/slots/hats.png','/outfits/slots/face-accessories.png','/outfits/slots/eyewear.png'][index]);
 }));
 for (const icon of await page.locator('.appearance-controls img').all()) await expect.poll(() => icon.evaluate(img => img.complete && img.naturalWidth > 0)).toBe(true);
 async function centered() { return page.locator('.appearance-controls').evaluate(el => {
   const group = el.getBoundingClientRect(), preview = el.closest('.fitting-room').getBoundingClientRect();
   return Math.abs(group.x + group.width / 2 - preview.x - preview.width / 2) < 1 && group.top > preview.top && group.top - preview.top < 15 && group.bottom < preview.bottom;
 }); }
 await check('appearance buttons are centered at the top inside the preview', await centered());
 async function sameSize() { return page.locator('.fitting-room').evaluate(el => {
 const rail = el.querySelector('.slot-rail .equipment-slot').getBoundingClientRect();
 return [...el.querySelectorAll('.appearance-slot')].every(slot => { const r = slot.getBoundingClientRect(); return r.width === rail.width && r.height === rail.height; });
 }); }
 await check('desktop appearance slots match equipment slot dimensions', await sameSize());
 await page.locator('.fitting-room').screenshot({path:evidence+'/empty-preview.png'});
 await page.locator('.appearance-controls').screenshot({ path: evidence + '/empty.png' });
 for (const [name,id] of [['Hair',10200070],['Makeup',10400011],['Face',10300003]]) {
  await equip(name,id);
  await expect(page.getByRole('heading', { name, exact: true })).toBeVisible();
  await expect.poll(() => slot(name).locator('.worn-icon').evaluate(img => img.complete && img.naturalWidth > 0)).toBe(true);
  await check(name + ' opens its picker and displays the selected thumbnail', await slot(name).locator('svg').count() === 0 && await slot(name).locator('.worn-icon').count() === 1);
 }
 await page.locator('.appearance-controls').screenshot({ path: evidence + '/selected.png' });
 await slot('Makeup').click(); await page.locator('.customize summary').click();
 await expect(page.getByLabel('Makeup placement', { exact: true })).toBeVisible();
 await page.getByLabel('Makeup placement', { exact: true }).selectOption('1');
 await check('makeup icon leads to working customization', await page.getByLabel('Makeup placement', { exact: true }).inputValue() === '1');
 await page.locator('.selected-title button').click(); await ready();
 await check('removing makeup restores the lollipop tile', await slot('Makeup').getAttribute('aria-label') === 'Makeup: Empty' && await slot('Makeup').locator('img').getAttribute('src') === '/outfits/slots/face-accessories.png');
 await equip('Makeup',10400011);
 await slot('Hair').focus(); await page.keyboard.press('Enter');
 await check('keyboard activation retains accessible names and focus', await slot('Hair').evaluate(el => el.matches(':focus-visible') && el.getAttribute('aria-pressed') === 'true'));
 await check('appearance tooltip appears below its slot within the preview', await slot('Hair').evaluate(el => { const tip = el.querySelector('.slot-tooltip').getBoundingClientRect(), slot = el.getBoundingClientRect(), preview = el.closest('.fitting-room').getBoundingClientRect(); return tip.top >= slot.bottom && tip.bottom < preview.bottom; }));
 await page.locator('.page-header').scrollIntoViewIfNeeded();
 await page.screenshot({ path: evidence + '/desktop.png' });
 await page.setViewportSize({ width: 390, height: 844 });
 await page.locator('.appearance-controls').scrollIntoViewIfNeeded();
 await page.screenshot({ path: evidence + '/mobile.png' });
 await check('mobile keeps the appearance buttons centered at the top inside the preview', await centered());
 await check('mobile appearance slots match equipment slot dimensions', await sameSize());
 await slot('Face').click();
 await expect(page.getByRole('heading', { name: 'Face', exact: true })).toBeVisible();
 await check('mobile icons open the correct panel without horizontal overflow', await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
 await check('no browser errors', errors.length === 0);
} catch (error) {
 errors.push(error.stack ?? String(error)); process.exitCode = 1;
 await page.screenshot({ path: evidence + '/failure.png', timeout: 10000 }).catch(() => {});
} finally {
 writeFileSync(evidence + '/report.json', JSON.stringify({ checks, errors }, null, 2));
 console.log(JSON.stringify({ checks: checks.length, errors }, null, 2)); await browser.close();
}
