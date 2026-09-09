// Exercise both Chilly glasses identities through a compiled local picker.
import { createRequire } from 'node:module';
import { mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
const { chromium, expect: baseExpect } = createRequire(process.env.HANDBOOK_FRONTEND + '/package.json')('@playwright/test');
const expect = baseExpect.configure({ timeout: 120000 });
const base = process.env.MIGRATION_URL ?? 'http://127.0.0.1:4011';
const output = resolve(process.env.MIGRATION_EVIDENCE);
mkdirSync(output, { recursive: true });
const browser = await chromium.launch({ executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH,
  args: ['--no-sandbox', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
const errors = [], failed = [], equipped = [];
try {
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
  page.on('pageerror', error => errors.push(error.message));
  page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });
  page.on('response', response => { if (response.status() >= 400) failed.push([response.status(), response.url()]); });
  await page.route('**/*', route => {
    const request = route.request();
    if (!['GET', 'HEAD'].includes(request.method()) || new URL(request.url()).origin !== base)
      return route.fulfill({ contentType: 'application/json', body: '{}' });
    return route.continue();
  });
  const name = 'Chilly Round Chain Glasses (F)';
  await page.goto(base + '/outfits');
  const ready = () => expect(page.getByRole('button', { name: 'Save image', exact: true })).toBeEnabled();
  await ready();
  await page.getByLabel('Body', { exact: true }).selectOption('female');
  await ready();
  for (const id of [11120100, 11150058]) {
    const response = await page.request.get(`${base}/api/outfits?body=female&slot=EY&availability=preview&search=${id}`);
    expect(response.status()).toBe(200);
    const data = await response.json();
    expect(data.total).toBe(1);
    expect(data.items[0].id).toBe(id);
    expect(data.items[0].library.parts).toHaveLength(1);
    await page.getByRole('button', { name: /^Eyewear:/ }).first().click();
    await page.getByRole('searchbox', { name: 'Find an item', exact: true }).fill(String(id));
    const choice = page.getByRole('button', { name: 'Equip ' + name, exact: true });
    await expect(choice).toBeEnabled();
    await choice.click();
    await ready();
    await expect(choice).toHaveAttribute('aria-pressed', 'true');
    await expect(page.getByRole('button', { name: 'Remove ' + name, exact: true })).toBeVisible();
    await page.locator('[aria-label="Outfit preview"]').screenshot({ path: resolve(output, id + '.png') });
    equipped.push(id);
  }
  const download = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Save image', exact: true }).click();
  await (await download).saveAs(resolve(output, 'glasses-export.png'));
  expect(errors).toEqual([]);
  expect(failed).toEqual([]);
} finally {
  await browser.close();
  writeFileSync(resolve(output, 'results.json'), JSON.stringify({ equipped, errors, failed }, null, 2));
}
console.log(JSON.stringify({ equipped, errors, failed }));
