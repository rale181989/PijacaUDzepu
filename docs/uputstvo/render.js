// HTML pregled slajdova -> PNG, uz proveru teksta koji izlazi iz okvira
const puppeteer = require('puppeteer-core');
const fs = require('fs');
const path = require('path');
const { pathToFileURL } = require('url');

(async () => {
  const dir = path.join(__dirname, 'preview');
  const b = await puppeteer.launch({ executablePath: 'C:/Program Files/Google/Chrome/Application/chrome.exe', headless: true, args: ['--allow-file-access-from-files'] });
  const p = await b.newPage();
  await p.setViewport({ width: 1280, height: 720 });
  const only = process.argv.slice(2);
  for (const f of fs.readdirSync(dir).filter(f => f.endsWith('.html'))) {
    if (only.length && !only.some(o => f.includes(o))) continue;
    await p.goto(pathToFileURL(path.join(dir, f)).href, { waitUntil: 'load' });
    const over = await p.evaluate(() => [...document.querySelectorAll('#s > div')]
      .filter(d => d.textContent.trim() && (d.scrollHeight > d.clientHeight + 2 || d.scrollWidth > d.clientWidth + 2))
      .map(d => d.textContent.trim().slice(0, 50) + ` [${d.scrollHeight}>${d.clientHeight}]`));
    if (over.length) console.log(f, 'OVERFLOW', over);
    await p.screenshot({ path: path.join(dir, f.replace('.html', '.png')) });
  }
  await b.close();
})();
