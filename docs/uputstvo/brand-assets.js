// Ikonice i slike aplikacije iz loga (PijacaUDzepu.Client/src/assets/logo.jpg):
// Android ikonice (obične, okrugle, prilagodljive), ekran pri pokretanju, favicon, PWA ikonice i slika za deljenje linka.
// Pokretanje (iz ovog foldera): node brand-assets.js   - posle toga: npm run build:android i novi APK
const sharp = require('sharp');
const path = require('path');

const CLIENT = path.join(__dirname, '..', '..', 'PijacaUDzepu.Client');
const LOGO = path.join(CLIENT, 'src', 'assets', 'logo.jpg');
const RES = path.join(CLIENT, 'android', 'app', 'src', 'main', 'res');
const ICONS = path.join(CLIENT, 'src', 'assets', 'icon');
const WHITE = { r: 255, g: 255, b: 255, alpha: 1 };

// Grafika bez teksta (telefon sa korpom, list i sunce) i ceo logo sa natpisom
const graphic = () => sharp(LOGO).extract({ left: 488, top: 82, width: 432, height: 432 });
const fullLogo = () => sharp(LOGO).extract({ left: 385, top: 75, width: 640, height: 620 });

// Grafika na belom kvadratu; scale = koliki deo kvadrata zauzima
async function square(size, scale) {
  const inner = Math.round(size * scale);
  const art = await graphic().resize(inner, inner).toBuffer();
  return sharp({ create: { width: size, height: size, channels: 4, background: WHITE } })
    .composite([{ input: art, gravity: 'center' }]);
}

async function rounded(size, radius) {
  const mask = Buffer.from(`<svg width="${size}" height="${size}"><rect width="${size}" height="${size}" rx="${radius}" ry="${radius}"/></svg>`);
  const base = await (await square(size, 0.84)).png().toBuffer();
  return sharp(base).composite([{ input: mask, blend: 'dest-in' }]);
}

async function circle(size) {
  const mask = Buffer.from(`<svg width="${size}" height="${size}"><circle cx="${size / 2}" cy="${size / 2}" r="${size / 2}"/></svg>`);
  const base = await (await square(size, 0.74)).png().toBuffer();
  return sharp(base).composite([{ input: mask, blend: 'dest-in' }]);
}

(async () => {
  // Android ikonice: legacy 48 dp, prilagodljiva 108 dp (vidljiv je srednji krug ~66%, pa grafika zauzima 60%)
  const densities = { mdpi: 1, hdpi: 1.5, xhdpi: 2, xxhdpi: 3, xxxhdpi: 4 };
  for (const [d, k] of Object.entries(densities)) {
    const dir = path.join(RES, `mipmap-${d}`);
    await (await rounded(48 * k, 8 * k)).png().toFile(path.join(dir, 'ic_launcher.png'));
    await (await circle(48 * k)).png().toFile(path.join(dir, 'ic_launcher_round.png'));
    await (await square(108 * k, 0.6)).png().toFile(path.join(dir, 'ic_launcher_foreground.png'));
  }

  // Ekran pri pokretanju: ceo logo na belom, u svim veličinama koje Capacitor već koristi
  const splashes = [
    ['drawable', 480, 320], ['drawable-land-mdpi', 480, 320], ['drawable-land-hdpi', 800, 480],
    ['drawable-land-xhdpi', 1280, 720], ['drawable-land-xxhdpi', 1600, 960], ['drawable-land-xxxhdpi', 1920, 1280],
    ['drawable-port-mdpi', 320, 480], ['drawable-port-hdpi', 480, 800], ['drawable-port-xhdpi', 720, 1280],
    ['drawable-port-xxhdpi', 960, 1600], ['drawable-port-xxxhdpi', 1280, 1920]
  ];
  for (const [dir, w, h] of splashes) {
    const logoW = Math.round(Math.min(w, h) * 0.55);
    const art = await fullLogo().resize(logoW).toBuffer();
    await sharp({ create: { width: w, height: h, channels: 4, background: WHITE } })
      .composite([{ input: art, gravity: 'center' }]).png().toFile(path.join(RES, dir, 'splash.png'));
  }

  // Sajt: favicon, ikonice za instalaciju (PWA) i iPhone početni ekran
  await (await rounded(64, 12)).png().toFile(path.join(ICONS, 'favicon.png'));
  await (await rounded(192, 32)).png().toFile(path.join(ICONS, 'icon-192.png'));
  await (await rounded(512, 84)).png().toFile(path.join(ICONS, 'icon-512.png'));
  await (await square(512, 0.66)).png().toFile(path.join(ICONS, 'icon-maskable-512.png'));
  await (await square(180, 0.84)).png().toFile(path.join(ICONS, 'apple-touch-icon.png'));

  // Slika za pregled linka (WhatsApp, Viber, Facebook): 1200x630
  const og = await fullLogo().resize(null, 540).toBuffer();
  await sharp({ create: { width: 1200, height: 630, channels: 4, background: WHITE } })
    .composite([{ input: og, gravity: 'center' }]).jpeg({ quality: 88 }).toFile(path.join(CLIENT, 'src', 'assets', 'og-image.jpg'));

  console.log('ikonice, ekran pri pokretanju, favicon, PWA ikonice i og-image napravljeni');
})();
