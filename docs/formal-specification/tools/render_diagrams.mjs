import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import puppeteer from 'puppeteer';
import crypto from 'node:crypto';

const here = path.dirname(fileURLToPath(import.meta.url));
const base = path.dirname(here);
const items = JSON.parse(await fs.readFile(path.join(base, 'validation/diagrams/input.json'), 'utf8'));
const browser = await puppeteer.launch({ headless: true });
try {
  const page = await browser.newPage();
  await page.setViewport({ width: 1600, height: 1200, deviceScaleFactor: 2 });
  await page.setContent('<html><body style="margin:0;background:white"></body></html>');
  await page.addScriptTag({ path: path.join(here, 'node_modules/mermaid/dist/mermaid.min.js') });
  await page.evaluate(() => mermaid.initialize({ startOnLoad: false, theme: 'neutral',
    securityLevel: 'loose', fontFamily: 'Arial', themeVariables: { fontSize: '17px' } }));
  for (const item of items) {
    const stamp = path.join(base, 'validation/diagrams', item.id + '.sha256');
    const digest = crypto.createHash('sha256').update(item.source).digest('hex');
    try { if ((await fs.readFile(stamp, 'utf8')) === digest) {
      console.log(`${item.id}: cached ${item.file}`); continue;
    } } catch {}
    const svg = await page.evaluate(async ({id, source}) => (await mermaid.render(id, source)).svg, item);
    await fs.writeFile(path.join(base, 'validation/diagrams', item.id + '.svg'), svg);
    await page.evaluate(svg => { document.body.innerHTML = svg;
      const element = document.querySelector('svg'); const box = element.viewBox.baseVal;
      element.style.maxWidth = 'none'; element.style.width = box.width + 'px';
      element.style.height = box.height + 'px'; }, svg);
    await (await page.$('svg')).screenshot({ path: path.join(base, 'validation/diagrams', item.id + '.png') });
    await fs.writeFile(stamp, digest);
    console.log(`${item.id}: ${item.file}`);
  }
} finally { await browser.close(); }
