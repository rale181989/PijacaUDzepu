// Snimci za slajd "Omiljeni, ocene i poruči ponovo" (f-favorites, f-rating). Menja LOKALNU bazu: Markove kruške na kratko
// postaju nedostupne i pravi se jedna završena porudžbina kupca. Pokretanje: node shots-customer.js
const puppeteer = require('puppeteer-core');
const path = require('path');
const API = 'http://localhost:5000/api', APP = 'http://localhost:8100';
const sleep = ms => new Promise(r => setTimeout(r, ms));
const call = async (method, url, body, token) => {
  const r = await fetch(API + url, { method, headers: { 'Content-Type': 'application/json', 'X-Forwarded-For': '10.40.0.' + Math.floor(Math.random() * 250), ...(token ? { Authorization: 'Bearer ' + token } : {}) }, body: body ? JSON.stringify(body) : undefined });
  const t = await r.text();
  return t ? JSON.parse(t) : null;
};

(async () => {
  const kupac = await call('POST', '/auth/login', { userName: 'kupac', password: 'kupac1234' });
  const marko = await call('POST', '/auth/login', { userName: 'marko', password: 'vendor1234' });
  // čista polazna tačka: kupac bez omiljenih
  const fav = await call('GET', '/favorites', null, kupac.token);
  for (const id of fav.productIds) await call('DELETE', '/favorites/products/' + id, null, kupac.token);
  for (const id of fav.vendorIds) await call('DELETE', '/favorites/vendors/' + id, null, kupac.token);

  const b = await puppeteer.launch({ executablePath: 'C:/Program Files/Google/Chrome/Application/chrome.exe', headless: true });
  const page = await b.newPage();
  const errors = [];
  page.on('console', m => { if (m.type() === 'error' && !m.text().includes('vibrate')) errors.push(m.text()); });
  await page.setViewport({ width: 390, height: 844, deviceScaleFactor: 2, isMobile: true, hasTouch: true });
  await page.goto(APP + '/home', { waitUntil: 'networkidle2' });
  await page.evaluate(() => localStorage.clear());
  await page.goto(APP + '/home', { waitUntil: 'networkidle2' });
  await sleep(1500);

  // gost: srce na prvom proizvodu (Beli Luk ili sl.) i prodavcu drugog
  const names = await page.evaluate(() => [...document.querySelectorAll('.product-name')].slice(0, 2).map(e => e.textContent.trim()));
  await page.evaluate(() => document.querySelectorAll('.fav-btn')[0].click());
  await page.evaluate(() => document.querySelectorAll('.fav-vendor')[1].click());
  console.log('gost označio:', names[0], '+ prodavac drugog proizvoda; localStorage:', await page.evaluate(() => localStorage.getItem('favorites')));

  // prijava -> sinhronizacija
  await page.evaluate(a => { localStorage.setItem('token', a.token); localStorage.setItem('user', JSON.stringify(a.user)); }, kupac);
  await page.goto(APP + '/home', { waitUntil: 'networkidle2' });
  await sleep(2000);
  console.log('posle prijave na serveru:', JSON.stringify(await call('GET', '/favorites', null, kupac.token)), '| lokalno:', await page.evaluate(() => localStorage.getItem('favorites')));

  // Markove kruške u omiljene, pa nedostupne
  await call('PUT', '/favorites/products/2', null, kupac.token);
  const p2 = await call('GET', '/products/2');
  await call('PUT', '/products/2', { name: p2.name, price: p2.price, unit: p2.unit, category: p2.category, productClass: p2.productClass, note: p2.note, isAvailable: false }, marko.token);
  await page.goto(APP + '/home', { waitUntil: 'networkidle2' });
  await sleep(1500);
  await page.evaluate(() => document.querySelector('.fav-filter').click());
  await sleep(2000);
  console.log('filter Omiljeni:', await page.evaluate(() => [...document.querySelectorAll('.product-name')].map(e => e.textContent.trim()).join(', ')));
  await page.evaluate(() => [...document.querySelectorAll('.out-of-stock ion-button')][0]?.click());
  await sleep(1200);
  console.log('obaveštenje traženo:', JSON.stringify((await call('GET', '/favorites', null, kupac.token)).alertProductIds));
  await page.screenshot({ path: path.join(__dirname, 'shots', 'f-favorites.png') });
  await call('PUT', '/products/2', { name: p2.name, price: p2.price, unit: p2.unit, category: p2.category, productClass: p2.productClass, note: p2.note, isAvailable: true }, marko.token);
  await sleep(1500);
  console.log('toast posle vraćanja u ponudu:', await page.evaluate(() => [...document.querySelectorAll('ion-toast')].map(t => t.message).join(' | ')));

  // ocena + poruči ponovo
  const [o] = await call('POST', '/orders', { items: [{ productId: 1, quantity: 2 }, { productId: 6, quantity: 1 }], fulfillment: 'Pickup', contactPhone: '0641234567' }, kupac.token);
  for (const st of ['Confirmed', 'ReadyForPickup', 'Completed']) await call('PUT', `/orders/${o.id}/status`, { status: st }, marko.token);
  await page.goto(APP + '/order-detail/' + o.id, { waitUntil: 'networkidle2' });
  await sleep(1500);
  await page.evaluate(() => [...document.querySelectorAll('.detail-actions ion-button')][0].click());
  await sleep(1500);
  const stars = await page.$$('app-rating-form .stars button');
  await stars[3].click();
  await page.type('app-rating-form ion-textarea textarea', 'Sveže i na vreme');
  await page.screenshot({ path: path.join(__dirname, 'shots', 'f-rating.png') });
  await page.evaluate(() => [...document.querySelectorAll('app-rating-form ion-button')].pop().click());
  await sleep(2000);
  console.log('ocena na stranici:', await page.evaluate(() => document.querySelector('.rating-card')?.innerText.replace(/\s+/g, ' ')));

  await page.evaluate(() => localStorage.removeItem('cart'));
  await page.evaluate(() => [...document.querySelectorAll('.detail-actions ion-button')].find(b => b.textContent.includes('ponovo')).click());
  await sleep(3000);
  console.log('poruči ponovo -> stranica:', new URL(page.url()).pathname, '| korpa:', await page.evaluate(() => JSON.parse(localStorage.getItem('cart') || '[]').map(i => i.product.name + ' x' + i.quantity).join(', ')));
  console.log('greške u konzoli:', errors.join('\n') || 'nema');
  await b.close();
})().catch(e => { console.error(e); process.exit(1); });
