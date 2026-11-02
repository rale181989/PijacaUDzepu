// Snimci za slajdove prodavca "Stanje i brza izmena cene" i "Pregled dana" (v-stock, v-quick, v-today, c-stock).
// Menja LOKALNU bazu: paprikama postavlja stanje 2 kg i pravi jednu porudžbinu kupca; posle vraća paprike
// na "stanje se ne prati". Pokretanje: node shots-vendor.js  (API na :5000, aplikacija na :8100)
const puppeteer = require('puppeteer-core');
const path = require('path');
const API = 'http://localhost:5000/api', APP = process.env.APP || 'http://localhost:8100';
const OUT = path.join(__dirname, 'shots');
const PAPRIKE = 5;
const sleep = ms => new Promise(r => setTimeout(r, ms));
let n = 0;
const call = async (method, url, body, token) => {
  const r = await fetch(API + url, { method, headers: { 'Content-Type': 'application/json', 'X-Forwarded-For': `10.60.${n++ % 250}.1`, ...(token ? { Authorization: 'Bearer ' + token } : {}) }, body: body ? JSON.stringify(body) : undefined });
  const t = await r.text();
  return t ? JSON.parse(t) : null;
};

(async () => {
  for (let i = 0; i < 60; i++) { try { if ((await fetch(API + '/auth/health')).ok) break; } catch {} await sleep(1000); }
  const kupac = await call('POST', '/auth/login', { userName: 'kupac', password: 'kupac1234' });
  const marko = await call('POST', '/auth/login', { userName: 'marko', password: 'vendor1234' });
  await call('PATCH', `/products/${PAPRIKE}/quick`, { stockQuantity: 2 }, marko.token);
  await call('POST', '/orders', { items: [{ productId: 1, quantity: 2 }, { productId: 6, quantity: 3 }], fulfillment: 'Pickup', contactPhone: '0641234567' }, kupac.token);

  const b = await puppeteer.launch({ executablePath: 'C:/Program Files/Google/Chrome/Application/chrome.exe', headless: true });
  const page = await b.newPage();
  await page.setViewport({ width: 390, height: 844, deviceScaleFactor: 2, isMobile: true, hasTouch: true });
  const login = async a => {
    await page.goto(APP + '/home', { waitUntil: 'networkidle2' });
    await page.evaluate(x => { localStorage.clear(); if (x) { localStorage.setItem('token', x.token); localStorage.setItem('user', JSON.stringify(x.user)); } }, a);
  };

  // kupac vidi stanje na kartici
  await login(null);
  await page.goto(APP + '/home', { waitUntil: 'networkidle2' });
  await sleep(1000);
  await page.type('.product-search input', 'paprike');
  await sleep(2500);
  await (await page.$('ion-card')).screenshot({ path: path.join(OUT, 'c-stock.png') });

  await login(marko);
  await page.goto(APP + '/vendor-products', { waitUntil: 'networkidle2' });
  await sleep(1500);
  // Slike koje ne postoje u lokalnoj bazi zamenjuje podrazumevana sličica
  await page.evaluate(() => document.querySelectorAll('img').forEach(img => { if (img.complete && img.naturalWidth === 0) img.src = 'assets/placeholder.svg'; }));
  await sleep(300);
  await page.screenshot({ path: path.join(OUT, 'v-stock.png') });
  const idx = await page.evaluate(() => [...document.querySelectorAll('app-vendor-products ion-item h2')].findIndex(h => h.textContent.includes('Paprike')));
  await (await page.$$('app-vendor-products ion-item ion-button[title]'))[idx].click();
  await sleep(1200);
  await page.screenshot({ path: path.join(OUT, 'v-quick.png') });
  await page.evaluate(() => document.querySelector('ion-alert')?.dismiss());

  await page.goto(APP + '/vendor-today', { waitUntil: 'networkidle2' });
  await sleep(1500);
  await page.screenshot({ path: path.join(OUT, 'v-today.png') });

  await call('PATCH', `/products/${PAPRIKE}/quick`, { clearStock: true, isAvailable: true }, marko.token);
  console.log('snimci: c-stock, v-stock, v-quick, v-today');
  await b.close();
})().catch(e => { console.error(e); process.exit(1); });
