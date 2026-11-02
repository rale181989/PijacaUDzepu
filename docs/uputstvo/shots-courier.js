// Snimci za slajd kurira "Više tezgi i obračun" (k-group, k-route, k-settlement).
// Menja LOKALNU bazu: kupac poruči od dva prodavca dostavu kurirom, kurir1 je prihvata ("Prihvati sve"),
// prodavci potvrde, a jednu porudžbinu kurir isporuči (da obračun ne bude prazan).
// Pokretanje: node shots-courier.js  (API na :5000, aplikacija na :8100)
const puppeteer = require('puppeteer-core');
const path = require('path');
const API = 'http://localhost:5000/api', APP = process.env.APP || 'http://localhost:8100';
const OUT = path.join(__dirname, 'shots');
const sleep = ms => new Promise(r => setTimeout(r, ms));
let n = 0;
const call = async (method, url, body, token) => {
  const r = await fetch(API + url, { method, headers: { 'Content-Type': 'application/json', 'X-Forwarded-For': `10.80.${n++ % 250}.1`, ...(token ? { Authorization: 'Bearer ' + token } : {}) }, body: body ? JSON.stringify(body) : undefined });
  const t = await r.text();
  return t ? JSON.parse(t) : null;
};

(async () => {
  for (let i = 0; i < 60; i++) { try { if ((await fetch(API + '/auth/health')).ok) break; } catch {} await sleep(1000); }
  const login = (u, p) => call('POST', '/auth/login', { userName: u, password: p });
  const kupac = await login('kupac', 'kupac1234'), marko = await login('marko', 'vendor1234');
  const jovana = await login('jovana', 'vendor1234'), kurir = await login('kurir1', 'kurir1234');
  await call('PUT', '/couriers/me/shift', { isOnShift: true }, kurir.token);
  const jovanaProduct = (await call('GET', '/products?vendorId=2&take=1')).items[0];
  const orders = await call('POST', '/orders', {
    items: [{ productId: 1, quantity: 2 }, { productId: jovanaProduct.id, quantity: 1 }],
    fulfillment: 'Courier', contactPhone: '0641234567', deliveryAddress: 'Bulevar oslobođenja 12, Novi Sad'
  }, kupac.token);

  const b = await puppeteer.launch({ executablePath: 'C:/Program Files/Google/Chrome/Application/chrome.exe', headless: true });
  const page = await b.newPage();
  const errors = [];
  page.on('console', m => { if (m.type() === 'error' && !m.text().includes('vibrate')) errors.push(m.text()); });
  await page.setViewport({ width: 390, height: 844, deviceScaleFactor: 2, isMobile: true, hasTouch: true });
  await page.goto(APP + '/home', { waitUntil: 'domcontentloaded' });
  await page.evaluate(a => { localStorage.clear(); localStorage.setItem('token', a.token); localStorage.setItem('user', JSON.stringify(a.user)); }, kurir);
  await page.goto(APP + '/courier-orders', { waitUntil: 'domcontentloaded' });
  await sleep(2000);
  await page.screenshot({ path: path.join(OUT, 'k-group.png') });

  await (await page.$('.group-banner ion-button')).click();
  await sleep(2000);
  await call('PUT', `/orders/${orders[0].id}/status`, { status: 'Confirmed' }, marko.token);
  await call('PUT', `/orders/${orders[1].id}/status`, { status: 'Confirmed' }, jovana.token);
  await page.reload({ waitUntil: 'domcontentloaded' });
  await sleep(1500);
  for (const btn of await page.$$('ion-segment-button')) if ((await btn.evaluate(e => e.getAttribute('value'))) === 'mine') await btn.click();
  await sleep(1200);
  await page.screenshot({ path: path.join(OUT, 'k-route.png') });

  // jedna isporuka, pa obračun
  await call('POST', `/couriers/orders/${orders[0].id}/picked-up`, null, kurir.token);
  await call('POST', `/couriers/orders/${orders[0].id}/delivered`, null, kurir.token);
  for (const btn of await page.$$('ion-segment-button')) if ((await btn.evaluate(e => e.getAttribute('value'))) === 'settlement') await btn.click();
  await sleep(1800);
  await page.screenshot({ path: path.join(OUT, 'k-settlement.png') });
  console.log('snimci: k-group, k-route, k-settlement; greške u konzoli:', errors.join('\n') || 'nema');
  await b.close();
})().catch(e => { console.error(e); process.exit(1); });
