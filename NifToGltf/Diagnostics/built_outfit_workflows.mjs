// Exercise the compiled public UI without development hooks or database writes.
import { createRequire } from 'node:module';
import { mkdirSync, writeFileSync } from 'node:fs';
const { chromium, expect: baseExpect } = createRequire(process.env.HANDBOOK_FRONTEND + '/package.json')('@playwright/test');
const expect = baseExpect.configure({ timeout: 60000 });
const output = process.env.WARDROBE_EVIDENCE_DIR;
if (!output) throw Error('WARDROBE_EVIDENCE_DIR is required');
mkdirSync(output, { recursive: true });
const url = process.env.WARDROBE_PREVIEW_URL ?? 'http://127.0.0.1:4003';
const browser = await chromium.launch({ headless: true, executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
page.setDefaultTimeout(60000);
const states = [], errors = [], blocked = [];
page.on('pageerror', e => errors.push(e.message));
await page.route('**/*', r => ['GET', 'HEAD'].includes(r.request().method()) ? r.continue() : (blocked.push(r.request().url()), r.abort()));
const preview = page.locator('[aria-label="Outfit preview"]');
const save = page.getByRole('button', { name: 'Save image', exact: true });
async function equip(id) {
  console.log('Equip', id);
  await page.getByRole('searchbox').fill(String(id));
  const button = page.getByRole('button', { name: /^Equip / }).filter({ hasText: String(id) }).first();
  await expect(button).toBeEnabled();
  const label = (await button.getAttribute('aria-label')).replace(/^Equip /, '').replace(/ in right hand$/, '');
  await button.click();
  await expect(page.getByRole('button', { name: 'Remove ' + label, exact: true })).toBeEnabled();
  await expect(save).toBeEnabled();
  return label;
}
async function capture(name) { await preview.screenshot({ path: `${output}/${name}.png` }); }
try {
  await page.goto(url + '/outfits', { waitUntil: 'domcontentloaded' });
  await expect(save).toBeEnabled();
  await page.locator('select').filter({ has: page.locator('option[value="verified"]') }).selectOption('preview');
  for (const body of ['female', 'male']) {
    await page.locator('select').filter({ has: page.locator('option[value="male"]') }).selectOption(body);
    await expect(save).toBeEnabled();
    const ids = [body === 'female' ? 10300003 : 10300001, body === 'female' ? 10200124 : 10200121, 11400367, 11500004, 11600004, 11700004, 11200001, 11300001, body === 'female' ? 10400011 : 10400002];
    const labels = [];
    for (const id of ids) labels.push(await equip(id));
    const expression = page.locator('select').filter({ has: page.locator('option[value="happy"]') });
    const expressions = await expression.locator('option').evaluateAll(options => options.map(o => o.value));
    for (const name of expressions) { await expression.selectOption(name); await capture(`${body}-expression-${name}`); }
    await expression.selectOption('happy');
    await page.locator('select').filter({ has: page.locator('option[value="fitting_idle_a"]') }).selectOption('fitting_idle_a');
    const pause = page.getByRole('button', { name: 'Pause', exact: true });
    if (await pause.count()) await pause.click();
    for (const angle of ['Front', 'Side', 'Back']) { await page.getByRole('button', { name: angle, exact: true }).click(); await capture(`${body}-${angle.toLowerCase()}`); }
    await page.getByLabel(/Makeup placement/).selectOption('1');
    await page.getByRole('button', { name: 'Reset makeup', exact: true }).click();
    const colors = page.getByText('Colors', { exact: true });
    if (!(await colors.evaluate(e => e.closest('details').open))) await colors.click();
    const group = page.getByRole('group', { name: labels[1], exact: true });
    await group.getByLabel(labels[1] + ' palette', { exact: true }).selectOption('1');
    const primary = group.getByLabel('Primary', { exact: true });
    await primary.fill('#123456');
    await primary.blur();
    await expect(primary).toHaveValue('#123456');
    await capture(`${body}-hair-dyed`);
    await equip(11300002); await equip(11300001);
    await group.getByRole('button', { name: 'Reset colors', exact: true }).click();
    const robe = await equip(12200002);
    for (const label of [labels[2], labels[3]]) await expect(page.getByRole('button', { name: 'Remove ' + label, exact: true })).toHaveCount(0);
    await capture(`${body}-full-outfit`);
    await equip(11500004);
    await expect(page.getByRole('button', { name: 'Remove ' + robe, exact: true })).toHaveCount(0);
    await equip(11400367);
    const download = page.waitForEvent('download'); await save.click(); await (await download).saveAs(`${output}/${body}-export.png`);
    await equip(11050055);
    const animation = page.locator('select').filter({ has: page.locator('option[value="Attack_Idle_A"]') });
    for (const name of ['Idle_A', 'Attack_Idle_A']) { await animation.selectOption(name); await expect(animation).toHaveValue(name); await capture(`${body}-nose-${name}`); }
    const headphones = await equip(11304858);
    await expect(page.getByRole('alert')).toHaveCount(0);
    await page.getByRole('button', { name: 'Remove ' + headphones, exact: true }).click();
    const adjustableHairId = body === 'female' ? 10200006 : 10200001;
    await equip(adjustableHairId);
    const lengthSelectors = page.locator('label').filter({ hasText: /length [0-9]/ }).locator('select');
    await expect(lengthSelectors.first()).toBeVisible();
    const lengthCount = await lengthSelectors.count();
    if (!lengthCount) throw Error('Expected adjustable hair controls for ' + body);
    for (let index = 0; index < lengthCount; index++) {
      const selector = lengthSelectors.nth(index);
      const initial = await selector.inputValue();
      const value = await selector.locator('option:not(:disabled)').last().getAttribute('value');
      await selector.selectOption(value);
      await capture(`${body}-length-${index}-changed`);
      await page.getByRole('button', { name: /^Reset .* length [0-9]+$/ }).nth(index).click();
      await expect(selector).toHaveValue(initial);
      await capture(`${body}-length-${index}-reset`);
    }
    await expect(page.getByRole('alert')).toHaveCount(0);
    states.push({ body, ids, labels, expressions, adjustableHairId, hairLengthControlsTested: lengthCount, equipped: await page.getByRole('button', { name: /^Remove / }).allTextContents() });
  }
  const absent = [];
  for (const path of ['/gltf/simulator-release-05/native-manifest.json', '/gltf/character-previews/gelo-07/native-manifest.json', '/gltf/character-previews/gelo-07/character.json']) {
    const response = await page.request.get(url + path); absent.push({ path, status: response.status() });
    if (response.status() !== 404) throw Error('Unselected or private artifact exposed: ' + path);
  }
  states.push({ publicationIsolation: absent });
  if (errors.length) throw Error(errors.join('\n'));
  console.log(JSON.stringify({ completeBodies: 2, errors, blocked }));
} finally {
  writeFileSync(output + '/results.json', JSON.stringify({ states, errors, blocked }, null, 2));
  writeFileSync(output + '/last-dom.txt', await page.locator('body').innerText());
  await page.screenshot({ path: output + '/last.png', fullPage: true });
  await browser.close();
}
