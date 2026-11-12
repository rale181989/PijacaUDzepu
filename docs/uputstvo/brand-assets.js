// Ikonice i slike aplikacije iz loga (PijacaUDzepu.Client/src/assets/logo.png: zeleni krug sa tezgom na telefonu,
// providna pozadina, 512x512): Android ikonice (obične, okrugle, prilagodljive), ekran pri pokretanju, favicon,
// PWA ikonice i slika za deljenje linka.
// Pokretanje (iz ovog foldera): node brand-assets.js   - posle toga: npm run build:android i novi APK
const sharp = require('sharp');
const fs = require('fs');
const path = require('path');

const CLIENT = path.join(__dirname, '..', '..', 'PijacaUDzepu.Client');
const LOGO = path.join(CLIENT, 'src', 'assets', 'logo.png');
const RES = path.join(CLIENT, 'android', 'app', 'src', 'main', 'res');
const ICONS = path.join(CLIENT, 'src', 'assets', 'icon');
const WHITE = { r: 255, g: 255, b: 255, alpha: 1 };
const CLEAR = { r: 0, g: 0, b: 0, alpha: 0 };
// Zeleni prelaz iz loga: svetlo gore desno, tamno dole levo
const LIGHT = '#A5CB52', DARK = '#488947';

const blank = (w, h, background) => sharp({ create: { width: w, height: h, channels: 4, background } });

// Ceo logo (krug) zauzima `scale` kvadrata, ostalo je providno ili belo
async function logoOn(size, scale, background = CLEAR) {
  const art = await sharp(LOGO).resize(Math.round(size * scale)).toBuffer();
  return blank(size, size, background).composite([{ input: art, gravity: 'center' }]);
}

// Zelena pozadina sa prelazom kao u logu (za prilagodljivu Android ikonicu, iPhone i maskable PWA)
function gradient(size) {
  return sharp(Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}">
    <defs><linearGradient id="g" x1="1" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="${LIGHT}"/><stop offset="1" stop-color="${DARK}"/></linearGradient></defs>
    <rect width="${size}" height="${size}" fill="url(#g)"/></svg>`));
}

// Samo beli crtež iz loga (tezga, telefon, jabuka, šargarepa) na providnom: providnost po tome koliko je piksel beo.
// Crtež je iste veličine kao u logu kada krug zauzima `circle` kvadrata.
async function glyph(size, circle) {
  const inner = Math.round(size * circle);
  const { data, info } = await sharp(LOGO).resize(inner).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const out = Buffer.alloc(data.length);
  const c = info.width / 2, r = info.width * 0.44;
  for (let i = 0; i < data.length; i += 4) {
    // Zelena pozadina ima nisku plavu komponentu (~70-90), beli crtež 255; svetla ivica kruga (van ~88% poluprečnika) se ne uzima
    const x = (i / 4) % info.width, y = Math.floor(i / 4 / info.width);
    const inside = Math.hypot(x - c, y - c) <= r;
    const a = inside ? Math.max(0, Math.min(1, (data[i + 2] - 140) / (245 - 140))) * (data[i + 3] / 255) : 0;
    out[i] = out[i + 1] = out[i + 2] = 255;
    out[i + 3] = Math.round(a * 255);
  }
  const art = await sharp(out, { raw: { width: info.width, height: info.height, channels: 4 } }).png().toBuffer();
  return blank(size, size, CLEAR).composite([{ input: art, gravity: 'center' }]);
}

// Beli crtež na zelenoj pozadini, ceo kvadrat (telefon sam seče oblik)
async function fullBleed(size, circle) {
  const bg = await gradient(size).png().toBuffer();
  const fg = await (await glyph(size, circle)).png().toBuffer();
  return sharp(bg).composite([{ input: fg }]);
}

(async () => {
  const densities = { mdpi: 1, hdpi: 1.5, xhdpi: 2, xxhdpi: 3, xxxhdpi: 4 };
  for (const [d, k] of Object.entries(densities)) {
    const dir = path.join(RES, `mipmap-${d}`);
    // Stari Android (pre 8.0): krug iz loga na providnom
    await (await logoOn(48 * k, 0.92)).png().toFile(path.join(dir, 'ic_launcher.png'));
    await (await logoOn(48 * k, 1)).png().toFile(path.join(dir, 'ic_launcher_round.png'));
    // Prilagodljiva ikonica 108 dp: zelena pozadina + beli crtež; vidi se srednjih ~72 dp, pa je krug loga 72/108
    await (await gradient(108 * k)).png().toFile(path.join(dir, 'ic_launcher_background.png'));
    await (await glyph(108 * k, 72 / 108)).png().toFile(path.join(dir, 'ic_launcher_foreground.png'));
  }
  // Pozadina prilagodljive ikonice je slika sa prelazom, ne bela boja
  for (const f of ['ic_launcher.xml', 'ic_launcher_round.xml']) {
    fs.writeFileSync(path.join(RES, 'mipmap-anydpi-v26', f), `<?xml version="1.0" encoding="utf-8"?>
<adaptive-icon xmlns:android="http://schemas.android.com/apk/res/android">
    <background android:drawable="@mipmap/ic_launcher_background"/>
    <foreground android:drawable="@mipmap/ic_launcher_foreground"/>
</adaptive-icon>
`);
  }

  // Ekran pri pokretanju: logo na belom, u svim veličinama koje Capacitor već koristi
  const splashes = [
    ['drawable', 480, 320], ['drawable-land-mdpi', 480, 320], ['drawable-land-hdpi', 800, 480],
    ['drawable-land-xhdpi', 1280, 720], ['drawable-land-xxhdpi', 1600, 960], ['drawable-land-xxxhdpi', 1920, 1280],
    ['drawable-port-mdpi', 320, 480], ['drawable-port-hdpi', 480, 800], ['drawable-port-xhdpi', 720, 1280],
    ['drawable-port-xxhdpi', 960, 1600], ['drawable-port-xxxhdpi', 1280, 1920]
  ];
  for (const [dir, w, h] of splashes) {
    const art = await sharp(LOGO).resize(Math.round(Math.min(w, h) * 0.42)).toBuffer();
    await blank(w, h, WHITE).composite([{ input: art, gravity: 'center' }]).png().toFile(path.join(RES, dir, 'splash.png'));
  }

  // Sajt: favicon i ikonice za instalaciju (PWA) - krug na providnom; maskable i iPhone bez providnosti
  await (await logoOn(64, 1)).png().toFile(path.join(ICONS, 'favicon.png'));
  await (await logoOn(192, 1)).png().toFile(path.join(ICONS, 'icon-192.png'));
  await (await logoOn(512, 1)).png().toFile(path.join(ICONS, 'icon-512.png'));
  await (await fullBleed(512, 0.72)).png().toFile(path.join(ICONS, 'icon-maskable-512.png'));
  await (await fullBleed(180, 0.9)).png().toFile(path.join(ICONS, 'apple-touch-icon.png'));

  // Slika za pregled linka (WhatsApp, Viber, Facebook): 1200x630, logo i naziv
  const og = await sharp(LOGO).resize(430).toBuffer();
  const title = Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="640" height="200">
    <text x="0" y="120" font-family="Arial, Helvetica, sans-serif" font-size="84" font-weight="700" fill="#2E7D32">Pijaca u Džepu</text></svg>`);
  await blank(1200, 630, WHITE)
    .composite([{ input: og, left: 70, top: 100 }, { input: title, left: 540, top: 215 }])
    .jpeg({ quality: 88 }).toFile(path.join(CLIENT, 'src', 'assets', 'og-image.jpg'));

  console.log('ikonice, ekran pri pokretanju, favicon, PWA ikonice i og-image napravljeni');
})();
