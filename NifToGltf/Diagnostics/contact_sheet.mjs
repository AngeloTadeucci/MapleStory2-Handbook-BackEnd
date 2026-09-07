// Compose captured images without altering individual pixels or inventing artwork.
import { createRequire } from 'node:module';
import { readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
const { chromium } = createRequire(process.env.HANDBOOK_FRONTEND + '/package.json')('@playwright/test');
const [directory] = process.argv.slice(2);
const { results } = JSON.parse(readFileSync(resolve(directory, 'results.json'), 'utf8'));
const browser = await chromium.launch({ executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH, headless: true });
try {
  const page = await browser.newPage();
  for (const body of ['female', 'male']) {
    const entries = results.filter(r => r.body === body).flatMap(r => r.captures.map(file => ({
      label: file.replace('.png', ''), ids: r.ids.join(', '),
      url: 'data:image/png;base64,' + readFileSync(resolve(directory, file)).toString('base64')
    })));
    if (!entries.length) continue;
    for (let offset = 0; offset < entries.length; offset += 12) {
    const data = await page.evaluate(async entries => {
      const canvas = document.createElement('canvas');
      canvas.width = 2000; canvas.height = Math.ceil(entries.length / 4) * 430;
      const context = canvas.getContext('2d');
      context.fillStyle = '#ddd'; context.fillRect(0, 0, canvas.width, canvas.height);
      for (const [index, entry] of entries.entries()) {
        const image = new Image(); image.src = entry.url; await image.decode();
        const x = index % 4 * 500, y = Math.floor(index / 4) * 430;
        const scale = Math.min(500 / image.width, 375 / image.height);
        context.drawImage(image, x + (500 - image.width * scale) / 2, y + 50, image.width * scale, image.height * scale);
        context.fillStyle = '#111'; context.font = '18px sans-serif'; context.fillText(entry.label, x + 8, y + 20);
        context.font = '9px monospace'; context.fillText(entry.ids, x + 8, y + 36, 480);
      }
      return canvas.toDataURL('image/png').split(',')[1];
    }, entries.slice(offset, offset + 12));
    writeFileSync(resolve(directory, `contact-${body}-${offset / 12 + 1}.png`), Buffer.from(data, 'base64'));
    }
  }
} finally { await browser.close(); }
