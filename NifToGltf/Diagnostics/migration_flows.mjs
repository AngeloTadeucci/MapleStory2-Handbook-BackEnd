// Compiled UI checks. GIF POSTs are intercepted and encoded locally with ffmpeg.
// No database writes, remote uploads, or development hooks are used.
import { createRequire } from 'node:module';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { execFileSync } from 'node:child_process';
const { chromium, expect: baseExpect } = createRequire(process.env.HANDBOOK_FRONTEND + '/package.json')('@playwright/test');
const expect = baseExpect.configure({ timeout: 120000 });
const output = resolve(process.env.MIGRATION_EVIDENCE);
const base = process.env.MIGRATION_URL ?? 'http://127.0.0.1:4009';
mkdirSync(output, { recursive: true });
const browser = await chromium.launch({ executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH,
  args: ['--no-sandbox', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
page.setDefaultTimeout(120000);
const errors = [], failed = [], models = [], states = [], gifs = [];
page.on('pageerror', error => errors.push(error.message));
page.on('response', response => {
  if (response.url().includes('/gltf/') && response.status() >= 400) failed.push([response.status(), response.url()]);
  if (response.url().endsWith('.gltf') && response.request().method() === 'GET') models.push(response.url());
});
await page.addInitScript(() => { window.open = () => null; });
await page.route('**/*', async route => {
  const request = route.request();
  if (new URL(request.url()).pathname === '/api/gif' && request.method() === 'POST') {
    const payload = request.postDataJSON();
    const directory = resolve(output, 'gif-' + gifs.length);
    mkdirSync(directory);
    for (const [index, screenshot] of payload.screenshots.entries()) {
      if (!screenshot.startsWith('data:image/png;base64,')) throw Error('Expected captured PNG');
      writeFileSync(resolve(directory, `frame${String(index).padStart(4, '0')}.png`), Buffer.from(screenshot.split(',')[1], 'base64'));
    }
    const file = resolve(directory, 'capture.gif');
    execFileSync('ffmpeg', ['-v', 'error', '-framerate', String(payload.framerate), '-i', resolve(directory, 'frame%04d.png'),
      '-vf', 'scale=320:-1', '-loop', '0', file], { timeout: 120000 });
    const bytes = readFileSync(file);
    if (!bytes.subarray(0, 6).toString().startsWith('GIF8')) throw Error('Local encoding failed');
    gifs.push({ model: payload.model, animation: payload.animation, frames: payload.screenshots.length, bytes: bytes.length });
    return route.fulfill(payload.download ? { contentType: 'image/gif', body: bytes } : { contentType: 'application/json', body: '{"url":"local-verification.gif"}' });
  }
  if (!['GET', 'HEAD'].includes(request.method())) return route.fulfill({ contentType: 'application/json', body: '{}' });
  if (new URL(request.url()).origin !== base && !request.url().startsWith('data:') && !request.url().startsWith('blob:'))
    return route.fulfill({ contentType: 'application/javascript', body: '' });
  return route.continue();
});
const save = page.getByRole('button', { name: 'Save image', exact: true });
const ready = () => expect(save).toBeEnabled();
async function equip(slot, id) {
  await page.getByRole('button', { name: new RegExp(`^${slot}:`) }).first().click();
  await page.getByRole('searchbox', { name: 'Find an item', exact: true }).fill(String(id));
  const choices = page.getByRole('button', { name: /^Equip / });
  await expect(choices).toHaveCount(1);
  const choice = choices.first();
  await expect(choice).toBeEnabled();
  const label = (await choice.getAttribute('aria-label')).replace(/^Equip /, '');
  await choice.click(); await ready();
  await expect(page.getByRole('button', { name: `Remove ${label}`, exact: true })).toBeVisible();
  return label;
}
async function capture(name) {
  await page.locator('[aria-label="Outfit preview"]').screenshot({ path: resolve(output, name + '.png') });
}
async function hairColorFields(hair) {
  await page.getByRole('button', { name: /^Hair:/ }).first().click();
  const details = page.locator('details.customize');
  if (!(await details.evaluate(node => node.open))) await details.locator('summary').click();
  const group = page.getByRole('group', { name: hair + ' colors', exact: true });
  await group.getByRole('radio', { name: 'Custom', exact: true }).check();
  return ['R', 'G', 'B'].map(channel => group.getByLabel(hair + ' Primary ' + channel, { exact: true }));
}
async function checkHairColor(hair) {
  const fields = await hairColorFields(hair);
  for (const [index, value] of ['18', '52', '86'].entries()) await expect(fields[index]).toHaveValue(value);
}
try {
  for (const [body, ids] of [['male', [11820357, 11850133]], ['female', [11820358, 11850134]]]) {
    const query = new URLSearchParams({ body, search: 'Olympus Divinity Wings', availability: 'preview' });
    const response = await page.request.get(base + '/api/outfits?' + query);
    expect(response.status()).toBe(200);
    const result = await response.json();
    for (const id of ids) expect(result.items.some(item => item.id === id && item.library.availability === 'preview')).toBe(true);
    states.push({ body, wingsSearch: true, itemIds: ids });
  }
  const manifest = await (await page.request.get(base + '/gltf/native-manifest.json')).json();
  const npcAsset = manifest.assets.find(asset => asset.id === '21000174_m_rabbitdollcymbalsgrey');
  const clipDuration = npcAsset.clipMetadata.find(clip => clip.name === 'Attack_01_A').duration;
  expect(clipDuration).toBeGreaterThan(0);
  await page.goto(base + '/npcs/21000174/model');
  await page.locator('canvas').waitFor();
  await expect(page.getByRole('combobox').first()).toBeEnabled();
  const initialRequests = models.length;
  await page.getByRole('combobox').first().click();
  await page.getByRole('option', { name: 'Attack_01_A', exact: true }).click();
  const speed = page.getByRole('slider', { name: 'Change animation speed', exact: true });
  await speed.focus();
  for (let step = 0; step < 10; step++) await speed.press('ArrowRight');
  await expect(speed).toHaveAttribute('aria-valuenow', '2');
  await page.getByRole('button', { name: 'Pause', exact: true }).click();
  const frame = page.getByRole('slider', { name: 'Frame:', exact: true });
  await frame.focus(); await frame.press('Home');
  await expect(frame).toHaveAttribute('aria-valuenow', '0');
  await expect(frame).toHaveAttribute('aria-valuemax', String(clipDuration));
  const before = await page.locator('canvas').evaluate(canvas => canvas.toDataURL());
  await page.getByRole('button', { name: 'Create GIF', exact: true }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Create', exact: true }).click();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  const after = await page.locator('canvas').evaluate(canvas => canvas.toDataURL());
  expect(after).toBe(before);
  expect(models.length).toBe(initialRequests);
  await expect(speed).toHaveAttribute('aria-valuenow', '2');
  expect(initialRequests).toBe(1);
  states.push({ npc: '21000174', oneModelRequest: true, selectedClip: 'Attack_01_A', capturedPoseRestored: true });
  await page.goto(base + '/outfits'); await ready();
  for (const body of ['female', 'male']) {
    await page.getByLabel('Body', { exact: true }).selectOption(body); await ready();
    const hair = await equip('Hair', body === 'male' ? 10200001 : 10200006);
    const fields = await hairColorFields(hair);
    for (const [index, value] of ['18', '52', '86'].entries()) {
      await fields[index].fill(value); await fields[index].blur();
    }
    await checkHairColor(hair);
    await capture(body + '-loose-dyed');
    const prior = models.length;
    const hat = await equip('Hats', 11300001);
    await checkHairColor(hair);
    await capture(body + '-hat-dyed');
    const alternateModel = body === 'male' ? '00200001_m_wolfhug_c' : '00200006_f_vanillagirl_c';
    const alternateRequests = models.slice(prior).filter(path => path.includes('/' + alternateModel + '/'));
    expect(alternateRequests.length).toBeGreaterThan(0);
    await page.getByRole('button', { name: 'Unequip Hats', exact: true }).click(); await ready();
    await checkHairColor(hair); await capture(body + '-restored-dyed');
    const download = page.waitForEvent('download'); await save.click();
    await (await download).saveAs(resolve(output, body + '-export.png'));
    states.push({ body, hair, hat, alternateRequests, dyePreserved: true, pngExport: true });
    const wingsId = body === 'male' ? 11850133 : 11850134;
    const wings = await equip('Back', wingsId);
    await capture(body + '-olympus-wings');
    states.push({ body, wings, itemId: wingsId, availableInPicker: true, equipped: true });
  }
  await page.setViewportSize({ width: 390, height: 844 });
  await ready();
  await page.screenshot({ path: resolve(output, 'mobile.png'), fullPage: true });
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390);
  expect(errors).toEqual([]); expect(failed).toEqual([]);
  console.log(JSON.stringify({ states, gifs, errors, failed }));
} finally {
  writeFileSync(resolve(output, 'results.json'), JSON.stringify({ states, gifs, errors, failed, models }, null, 2));
  writeFileSync(resolve(output, 'last-dom.txt'), await page.locator('body').innerText());
  await page.screenshot({ path: resolve(output, 'last.png'), fullPage: true });
  await browser.close();
}
