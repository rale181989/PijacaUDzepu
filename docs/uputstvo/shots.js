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

  // Kupac bez naloga (gost)
  await asUser(page, null);
  await shot(page, 'c-home', '/home');
  await shot(page, 'c-home-products', '/home', scrollContent(520));
  await shot(page, 'c-menu', '/home', openMenu);

  // Korpa sa proizvodima
  const products = await (await fetch('http://localhost:5000/api/products?take=50')).json();
  const pick = ids => ids.map(([id, q]) => ({ product: products.items.find(p => p.id === id), quantity: q })).filter(i => i.product);
  await page.evaluate(cart => localStorage.setItem('cart', JSON.stringify(cart)), pick([[1, 2], [4, 1], [6, 3]]));
  await shot(page, 'c-cart', '/cart');
  await shot(page, 'c-cart-checkout', '/cart', scrollContent(2000));

  // Praćenje porudžbine gosta
  await page.evaluate(code => { localStorage.removeItem('cart'); localStorage.setItem('guestOrders', JSON.stringify([code])); }, demo.trackingCode);
  await shot(page, 'c-track', '/prati/' + demo.trackingCode);
  await shot(page, 'c-login', '/login');
  await shot(page, 'c-support', '/podrska');

  // Prijavljeni kupac
  await asUser(page, demo.kupac);
  await shot(page, 'c-my-orders', '/my-orders');
  await shot(page, 'c-order-detail', '/order-detail/' + demo.orders[0]);

  // Prodavac
  await asUser(page, demo.marko);
  await shot(page, 'v-orders', '/vendor-orders');
  await shot(page, 'v-orders-more', '/vendor-orders', scrollContent(420));
  await shot(page, 'v-products', '/vendor-products');
  await shot(page, 'v-product-form', '/vendor-product-form/1');
  await shot(page, 'v-profile', '/vendor-profile');
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
