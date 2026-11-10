// Snimci ekrana aplikacije u veličini telefona (lokalni klijent na :4200)
const puppeteer = require('puppeteer-core');
const fs = require('fs');
const path = require('path');

const APP = process.env.APP || 'http://localhost:4200';
const OUT = path.join(__dirname, 'shots');
const demo = JSON.parse(fs.readFileSync(path.join(__dirname, 'demo.json'), 'utf8'));
const only = process.argv.slice(2);

const sleep = ms => new Promise(r => setTimeout(r, ms));

async function asUser(page, auth) {
  await page.goto(APP + '/home', { waitUntil: 'networkidle2' });
  await page.evaluate(a => {
    localStorage.clear();
    if (a) {
      localStorage.setItem('token', a.token);
      localStorage.setItem('user', JSON.stringify(a.user));
    }
  }, auth);
}

async function shot(page, name, url, prep) {
  if (only.length && !only.includes(name)) return;
  await page.goto(APP + url, { waitUntil: 'networkidle2' });
  await sleep(1500);
  // Slike koje ne postoje u lokalnoj bazi zamenjuje podrazumevana sličica, kao kod proizvoda bez slike
  await page.evaluate(() => document.querySelectorAll('img').forEach(img => {
    if (img.complete && img.naturalWidth === 0) img.src = 'assets/placeholder.svg';
  }));
  if (prep) { await prep(page); await sleep(1200); }
  await page.screenshot({ path: path.join(OUT, name + '.png') });
  console.log('shot', name);
}

const scrollContent = y => page => page.evaluate(async y => {
  const contents = [...document.querySelectorAll('ion-router-outlet ion-content')];
  const c = contents[contents.length - 1];
  if (c) await c.scrollToPoint(0, y, 0);
}, y);

const scrollToText = text => page => page.evaluate(async text => {
  const el = [...document.querySelectorAll('ion-router-outlet *')].reverse()
    .find(e => e.children.length === 0 && e.textContent && e.textContent.includes(text));
  const contents = [...document.querySelectorAll('ion-router-outlet ion-content')];
  const c = contents[contents.length - 1];
  if (el && c) {
    const scrollEl = await c.getScrollElement();
    const top = el.getBoundingClientRect().top - scrollEl.getBoundingClientRect().top + scrollEl.scrollTop - 90;
    await c.scrollToPoint(0, Math.max(0, top), 0);
  }
}, text);

const openMenu = page => page.evaluate(async () => { await document.querySelector('ion-menu')?.open(false); });

(async () => {
  fs.mkdirSync(OUT, { recursive: true });
  const browser = await puppeteer.launch({
    executablePath: 'C:/Program Files/Google/Chrome/Application/chrome.exe',
    headless: true,
    args: ['--lang=sr-Latn-RS', '--autoplay-policy=no-user-gesture-required']
  });
  const page = await browser.newPage();
  await page.setViewport({ width: 390, height: 844, deviceScaleFactor: 2, isMobile: true, hasTouch: true });

  // Kupac bez naloga (gost); okvir "Kako radi" je već zatvoren, da filteri stoje gde ih oznake pokazuju
  await asUser(page, null);
  await page.evaluate(() => localStorage.setItem('howItWorksSeen', '1'));
  await shot(page, 'c-home', '/home');
  await shot(page, 'c-home-products', '/home', scrollContent(520));
  await shot(page, 'c-menu', '/home', openMenu);

  // Korpa sa proizvodima
  const products = await (await fetch('http://localhost:5000/api/products?take=50')).json();
  const pick = ids => ids.map(([id, q]) => ({ product: products.items.find(p => p.id === id), quantity: q })).filter(i => i.product);
  await page.evaluate(cart => localStorage.setItem('cart', JSON.stringify(cart)), pick([[1, 2], [4, 1], [6, 3]]));
  await shot(page, 'c-cart', '/cart');
  await shot(page, 'c-cart-checkout', '/cart', scrollContent(2000));

  // Praćenje porudžbine gosta: nova kupovina sa dve tezge (demo porudžbina iz demo.json je možda već zatvorena)
  const guestOrders = await (await fetch('http://localhost:5000/api/orders/guest', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ items: [{ productId: 1, quantity: 2 }, { productId: products.items.find(p => p.vendorId === 2).id, quantity: 1 }],
      fulfillment: 'Courier', guestName: 'Petar Petrović', guestPhone: '060 123 4567', guestAddress: 'Dunavska 5, sprat 2, stan 7' })
  })).json();
  const trackingCode = guestOrders[0].trackingCode;
  await fetch(`http://localhost:5000/api/orders/${guestOrders[0].id}/status`, {
    method: 'PUT', headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + demo.marko.token }, body: JSON.stringify({ status: 'Confirmed', message: 'Spremamo, stiže za sat vremena' })
  });
  await page.evaluate(code => { localStorage.removeItem('cart'); localStorage.setItem('guestOrders', JSON.stringify([code])); }, trackingCode);
  await shot(page, 'c-track', '/prati/' + trackingCode);
  await shot(page, 'c-login', '/login');
  await shot(page, 'c-register', '/register', async p => {
    // Redom: ime, prezime, korisničko ime, lozinka, e-mail, telefon, adresa, sprat, stan
    const values = ['Petar', 'Petrović', 'petar', 'lozinka123', 'petar@example.com', '060 123 4567', 'Dunavska 5', '2', '7'];
    const inputs = await p.$$('.register-container ion-input input');
    for (let i = 0; i < values.length; i++) await inputs[i].type(values[i]);
  });
  await shot(page, 'a-forgot', '/forgot-password');
  await shot(page, 'c-support', '/podrska');

  // Prijavljeni kupac
  await asUser(page, demo.kupac);
  // Adresa za dostavu demo kupca (lokalna baza je možda starija od tih polja)
  await page.evaluate(token => fetch('http://localhost:5000/api/auth/profile', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + token },
    body: JSON.stringify({ phoneNumber: '060 123 4567', address: 'Dunavska 5', floor: '2', apartment: '7' })
  }), demo.kupac.token);
  await shot(page, 'c-my-orders', '/my-orders');
  await shot(page, 'c-order-detail', '/order-detail/' + demo.orders[0]);
  await shot(page, 'a-account', '/account');

  // Prodavac
  await asUser(page, demo.marko);
  await shot(page, 'v-orders', '/vendor-orders');
  // Prva potvrđena porudžbina (sa dugmetom "Spremno") na vrhu ekrana
  await shot(page, 'v-orders-more', '/vendor-orders', page => page.evaluate(async () => {
    const btn = [...document.querySelectorAll('app-vendor-orders ion-button')].find(b => b.textContent.includes('Spremno'));
    const card = btn?.closest('ion-card');
    const c = document.querySelector('app-vendor-orders ion-content');
    if (card && c) {
      const scrollEl = await c.getScrollElement();
      const top = card.getBoundingClientRect().top - scrollEl.getBoundingClientRect().top + scrollEl.scrollTop - 8;
      await c.scrollToPoint(0, Math.max(0, top), 0);
    }
    await new Promise(r => setTimeout(r, 400));
  }));
  await shot(page, 'v-products', '/vendor-products');
  await shot(page, 'v-product-form', '/vendor-product-form/1');
  await shot(page, 'v-profile', '/vendor-profile');
  // Radno vreme: privremeno 07-14 pon-sub i neradni dan za 3 dana, snimak, pa vraćanje bez ograničenja
  if (!only.length || only.includes('v-hours')) {
    const avail = body => fetch('http://localhost:5000/api/vendors/my-vendor/availability', {
      method: 'PUT', headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + demo.marko.token }, body: JSON.stringify(body)
    });
    const d = new Date(); d.setDate(d.getDate() + 3);
    await avail({ ordersPaused: false, openingHours: [1, 2, 3, 4, 5, 6].map(day => ({ day, from: '07:00', to: '14:00' })), closedDates: [d.toISOString().slice(0, 10)] });
    await shot(page, 'v-hours', '/vendor-profile', scrollToText('Radno vreme i dostupnost'));
    await avail({ ordersPaused: false, openingHours: [], closedDates: [] });
  }
  await shot(page, 'v-menu', '/vendor-orders', openMenu);

  // Kurir
  await asUser(page, demo.kurir);
  await shot(page, 'k-available', '/courier-orders');
  await shot(page, 'k-mine', '/courier-orders', async p => {
    await p.evaluate(() => {
      const seg = [...document.querySelectorAll('ion-segment-button')].find(b => b.getAttribute('value') === 'mine');
      seg?.click();
    });
  });
  await shot(page, 'k-profile', '/courier-profile');

  await browser.close();
})().catch(e => { console.error(e); process.exit(1); });
