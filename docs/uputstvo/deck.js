// Korisničko uputstvo "Pijaca u Džepu" -> Pijaca-u-Dzepu-uputstvo.pptx + preview/slide-N.html (ista geometrija, za vizuelnu proveru).
// Uputstvo za pokretanje je u README.md u ovom folderu.
const pptxgen = require('pptxgenjs');
const fs = require('fs');
const path = require('path');
const React = require('react');
const ReactDOMServer = require('react-dom/server');
const sharp = require('sharp');
const Md = require('react-icons/md');
const { applyTheme } = require('./apply_theme.js');

const SHOTS = path.join(__dirname, 'shots');
const OUT = process.argv[2] || path.join(__dirname, 'Pijaca-u-Dzepu-uputstvo.pptx');
const W = 13.333, H = 7.5;

const THEME = {
  name: 'Pijaca u Dzepu',
  headFontFace: 'Cambria',
  bodyFontFace: 'Calibri',
  colors: {
    dk1: '1B2A1E', lt1: 'FFFFFF', dk2: '1F5E2A', lt2: 'EAF4E7',
    accent1: '2E7D32', accent2: 'E8731C', accent3: 'D23C2A', accent4: '3B66C9', accent5: 'F2B705', accent6: '5F6B61',
    hlink: '2E7D32', folHlink: '1F5E2A'
  }
};
const pres = new pptxgen();
pres.layout = 'LAYOUT_WIDE';
pres.theme = { headFontFace: THEME.headFontFace, bodyFontFace: THEME.bodyFontFace };
pres.title = 'Pijaca u Džepu – korisničko uputstvo';
pres.author = 'Pijaca u Džepu';
const C = pres.SchemeColor;

// Ime boje -> [scheme boja za pptx, hex za HTML pregled]
const COL = {
  ink: [C.text1, THEME.colors.dk1], white: [C.background1, 'FFFFFF'], forest: [C.text2, THEME.colors.dk2],
  mint: [C.background2, THEME.colors.lt2], green: [C.accent1, THEME.colors.accent1], carrot: [C.accent2, THEME.colors.accent2],
  tomato: [C.accent3, THEME.colors.accent3], sky: [C.accent4, THEME.colors.accent4], sun: [C.accent5, THEME.colors.accent5],
  muted: [C.accent6, THEME.colors.accent6]
};
const ROLE = { kupac: ['KUPAC', 'carrot'], prodavac: ['PRODAVAC', 'green'], kurir: ['KURIR', 'sky'] };

// ---------- layouts ----------
const footer = [
  { text: { text: 'Pijaca u Džepu · Korisničko uputstvo', options: { x: 0.6, y: 7.0, w: 5, h: 0.3, fontSize: 10, color: C.accent6, margin: 0 } } }
];
const slideNum = { x: 12.23, y: 7.0, w: 0.5, h: 0.3, fontSize: 10, color: C.accent6, align: 'right', margin: 0 };
const titlePh = (w) => ({ placeholder: { options: { name: 'title', type: 'title', x: 0.6, y: 0.62, w, h: 0.8, fontSize: 30, bold: true, color: C.text2, fontFace: 'Cambria', valign: 'top', margin: 0 }, text: '' } });

pres.defineSlideMaster({ title: 'Naslovni', background: { color: C.text2 }, objects: [] });
pres.defineSlideMaster({ title: 'Odeljak', background: { color: C.text2 }, objects: [], slideNumber: { ...slideNum, color: C.background2 } });
pres.defineSlideMaster({ title: 'Sadrzaj - jedan ekran', background: { color: C.background1 }, objects: [...footer, titlePh(8.3)], slideNumber: slideNum });
pres.defineSlideMaster({ title: 'Sadrzaj - dva ekrana', background: { color: C.background1 }, objects: [...footer, titlePh(5.2)], slideNumber: slideNum });
pres.defineSlideMaster({ title: 'Sadrzaj - bez ekrana', background: { color: C.background1 }, objects: [...footer, titlePh(12.1)], slideNumber: slideNum });
const LAYOUT_BG = { 'Naslovni': 'forest', 'Odeljak': 'forest' };
const LAYOUT_TITLE_W = { 'Sadrzaj - jedan ekran': 8.3, 'Sadrzaj - dva ekrana': 5.2, 'Sadrzaj - bez ekrana': 12.1 };

// ---------- icons ----------
const iconCache = {};
async function icon(name, color) {
  const key = name + color;
  if (!iconCache[key]) {
    const svg = ReactDOMServer.renderToStaticMarkup(React.createElement(Md[name], { color: '#' + color, size: 256 }));
    iconCache[key] = (await sharp(Buffer.from(svg)).png().toBuffer()).toString('base64');
  }
  return iconCache[key];
}

// ---------- slide builder (pptx + html) ----------
const previews = [];
let objId = 0;

class S {
  constructor(layout, section, role) {
    this.layout = layout;
    this.s = pres.addSlide({ masterName: layout, sectionTitle: section });
    this.html = [];
    this.role = role;
    const bg = COL[LAYOUT_BG[layout] || 'white'][1];
    this.bg = bg;
    previews.push(this);
    if (!LAYOUT_BG[layout]) {
      this.html.push(div(0.6, 7.0, 5, 0.3, `font:10pt Calibri;color:#${COL.muted[1]}`, 'Pijaca u Džepu · Korisničko uputstvo'));
      this.html.push(div(12.23, 7.0, 0.5, 0.3, `font:10pt Calibri;color:#${COL.muted[1]};text-align:right`, String(previews.length)));
    } else if (layout === 'Odeljak') {
      this.html.push(div(12.23, 7.0, 0.5, 0.3, `font:10pt Calibri;color:#${COL.mint[1]};text-align:right`, String(previews.length)));
    }
    if (role) this.chip(ROLE[role][0], ROLE[role][1], 0.6, 0.28);
  }
  title(text) {
    this.s.addText(text, { placeholder: 'title' });
    this.html.push(div(0.6, 0.62, LAYOUT_TITLE_W[this.layout], 0.8, `font:bold 30pt Cambria;color:#${COL.forest[1]}`, text));
  }
  text(text, x, y, w, h, o = {}) {
    const color = COL[o.color || 'ink'];
    const runs = Array.isArray(text) ? text : [{ text }];
    this.s.addText(runs.map(r => ({ text: r.text, options: { bold: r.bold, color: r.color ? COL[r.color][0] : undefined, breakLine: r.breakLine } })), {
      x, y, w, h, isTextBox: true, margin: 0, valign: o.valign || 'top', align: o.align || 'left',
      fontSize: o.size || 14, bold: o.bold, color: color[0], fontFace: o.head ? 'Cambria' : undefined,
      paraSpaceAfter: o.paraAfter, objectName: `text-${++objId}`
    });
    const css = `font:${o.bold ? 'bold ' : ''}${o.size || 14}pt ${o.head ? 'Cambria' : 'Calibri'};color:#${color[1]};text-align:${o.align || 'left'};` +
      (o.valign === 'middle' ? 'display:flex;flex-direction:column;justify-content:center;' : '');
    const inner = runs.map(r => {
      const st = (r.bold ? 'font-weight:bold;' : '') + (r.color ? `color:#${COL[r.color][1]};` : '');
      return `<span style="${st}">${esc(r.text)}</span>${r.breakLine ? '<br>' : ''}`;
    }).join('');
    this.html.push(div(x, y, w, h, css, o.valign === 'middle' ? `<div>${inner}</div>` : inner, true));
  }
  shape(kind, x, y, w, h, fill, o = {}) {
    const opts = { x, y, w, h, fill: { color: COL[fill][0] }, line: { type: 'none' }, objectName: `${kind}-${++objId}` };
    if (kind === 'round') { opts.rectRadius = o.r ?? 0.12; }
    if (o.line) opts.line = { color: COL[o.line][0], width: o.lineW || 1.5 };
    if (o.shadow) opts.shadow = { type: 'outer', color: '000000', opacity: 0.25, blur: 8, offset: 3, angle: 90 };
    this.s.addShape(kind === 'oval' ? pres.shapes.OVAL : kind === 'round' ? pres.shapes.ROUNDED_RECTANGLE : pres.shapes.RECTANGLE, opts);
    const radius = kind === 'oval' ? '50%' : kind === 'round' ? `${(o.r ?? 0.12) * Math.min(w, h) * 96}px` : '0';
    this.html.push(div(x, y, w, h, `background:#${COL[fill][1]};border-radius:${radius};` +
      (o.line ? `box-shadow:inset 0 0 0 ${o.lineW || 1.5}pt #${COL[o.line][1]};` : '') + (o.shadow ? 'box-shadow:0 3px 8px rgba(0,0,0,.25);' : ''), ''));
  }
  chip(label, color, x, y) {
    const w = 0.22 + label.length * 0.095;
    this.shape('round', x, y, w, 0.3, color, { r: 0.5 });
    this.text(label, x, y, w, 0.3, { size: 10, bold: true, color: 'white', align: 'center', valign: 'middle' });
  }
  image(file, x, y, w, h, alt) {
    this.s.addImage({ path: file, x, y, w, h, altText: alt, objectName: `image-${++objId}` });
    this.html.push(`<img src="file:///${file.replace(/\\/g, '/')}" style="position:absolute;left:${x * 96}px;top:${y * 96}px;width:${w * 96}px;height:${h * 96}px">`);
  }
  async icon(name, x, y, d, circle, iconColor = 'FFFFFF') {
    this.shape('oval', x, y, d, d, circle);
    const pad = d * 0.22;
    const data = await icon(name, iconColor);
    this.s.addImage({ data: 'image/png;base64,' + data, x: x + pad, y: y + pad, w: d - 2 * pad, h: d - 2 * pad, altText: '', objectName: `icon-${++objId}` });
    this.html.push(`<img src="data:image/png;base64,${data}" style="position:absolute;left:${(x + pad) * 96}px;top:${(y + pad) * 96}px;width:${(d - 2 * pad) * 96}px">`);
  }
  // Telefon: tamni okvir + snimak ekrana + numerisani markeri (fx, fy = položaj na slici, 0..1)
  phone(name, x, y, h, alt, markers = []) {
    const pad = 0.1, imgH = h - 2 * pad, imgW = imgH * 390 / 844;
    this.shape('round', x, y, imgW + 2 * pad, h, 'ink', { r: 0.08, shadow: true });
    this.image(path.join(SHOTS, name + '.png'), x + pad, y + pad, imgW, imgH, alt);
    markers.forEach(([n, fx, fy]) => this.marker(n, x + pad + fx * imgW, y + pad + fy * imgH));
    return imgW + 2 * pad;
  }
  marker(n, cx, cy, d = 0.34) {
    this.shape('oval', cx - d / 2, cy - d / 2, d, d, 'carrot', { line: 'white', lineW: 2 });
    this.text(String(n), cx - d / 2, cy - d / 2, d, d, { size: 12, bold: true, color: 'white', align: 'center', valign: 'middle' });
  }
  // Numerisani koraci levo od telefona
  steps(items, x = 0.6, y = 1.75, w = 7.9, gap = 0.28) {
    let cy = y;
    for (const [i, it] of items.entries()) {
      const n = it.n ?? i + 1;
      this.marker(n, x + 0.2, cy + 0.19, 0.4);
      const tx = x + 0.62, tw = w - 0.62;
      this.text(it.h, tx, cy, tw, 0.34, { size: 17, bold: true, color: 'forest' });
      const lines = estLines(it.d, tw, 14);
      const dh = lines * 0.245;
      this.text(it.d, tx, cy + 0.38, tw, dh, { size: 14 });
      cy += 0.38 + dh + gap;
    }
    return cy;
  }
}

function estLines(text, w, pt) {
  const charW = pt / 72 * 0.5;
  const perLine = Math.floor(w / charW);
  let lines = 0;
  for (const para of text.split('\n')) {
    let cur = 0; lines++;
    for (const word of para.split(' ')) {
      if (cur && cur + 1 + word.length > perLine) { lines++; cur = word.length; } else cur += (cur ? 1 : 0) + word.length;
    }
  }
  return lines;
}
const esc = s => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/\n/g, '<br>');
const div = (x, y, w, h, css, content, raw) =>
  `<div style="position:absolute;left:${x * 96}px;top:${y * 96}px;width:${w * 96}px;height:${h * 96}px;line-height:1.2;${css}">${raw ? content : esc(content)}</div>`;

// ---------- content ----------
const PHONE_H = 6.3, PHONE_Y = 0.45;
const RIGHT_X = W - 0.75 - (PHONE_H - 0.2) * 390 / 844 - 0.2;
const LEFT_X = RIGHT_X - 0.35 - ((PHONE_H - 0.2) * 390 / 844 + 0.2);

async function build() {
  // 1. Naslovna
  pres.addSection({ title: 'Uvod' });
  let s = new S('Naslovni', 'Uvod');
  await s.icon('MdStorefront', 0.6, 1.25, 0.9, 'carrot');
  s.text('Pijaca u Džepu', 0.6, 2.35, 7.6, 1.0, { size: 54, bold: true, head: true, color: 'white' });
  s.text('Korisničko uputstvo', 0.6, 3.4, 7.6, 0.6, { size: 28, color: 'mint', head: true });
  s.text('Za kupce, prodavce na pijaci i Eko kurire. Svaki korak je prikazan na ekranu aplikacije, a brojevi na slikama odgovaraju brojevima u tekstu.', 0.6, 4.35, 7.0, 0.9, { size: 16, color: 'mint' });
  s.text('Novi Sad · novembar 2026.', 0.6, 6.55, 5, 0.35, { size: 12, color: 'mint' });
  s.phone('c-home', RIGHT_X, PHONE_Y, PHONE_H, 'Početni ekran aplikacije sa filterima i proizvodima');

  // 2. Uloge
  s = new S('Sadrzaj - bez ekrana', 'Uvod');
  s.title('Jedna aplikacija, tri uloge');
  const roles = [
    ['MdShoppingBasket', 'carrot', 'Kupac', 'Bira proizvode sa novosadskih pijaca i poručuje ih: sam preuzima robu na tezgi ili je dobija na adresu preko Eko kurira. Nalog nije obavezan.', 'Strane 3–11'],
    ['MdStorefront', 'green', 'Prodavac', 'Održava ponudu i cene, prima porudžbine, potvrđuje ih i javlja kupcu kada je roba spremna.', 'Strane 12–17'],
    ['MdDeliveryDining', 'sky', 'Eko kurir', 'Uključuje smenu, prihvata dostave, preuzima robu od prodavca i nosi je kupcu na adresu.', 'Strane 18–21']
  ];
  for (const [i, [ic, col, h, d, pages]] of roles.entries()) {
    const x = 0.6 + i * 4.1;
    s.shape('round', x, 1.75, 3.8, 3.85, 'mint', { r: 0.06 });
    await s.icon(ic, x + 0.35, 2.05, 0.95, col);
    s.text(h, x + 0.35, 3.2, 3.1, 0.45, { size: 22, bold: true, head: true, color: 'forest' });
    s.text(d, x + 0.35, 3.75, 3.15, 1.35, { size: 14 });
    s.text(pages, x + 0.35, 5.15, 3.1, 0.3, { size: 12, color: 'muted' });
  }
  await s.icon('MdLanguage', 0.6, 6.0, 0.5, 'forest');
  s.text([{ text: 'Gde radi: ', bold: true }, { text: 'u browseru na pijacaudzepu.onrender.com i kao Android aplikacija. Jezik (SR · EN · RU) birate u meniju ☰.' }], 1.25, 6.0, 11.3, 0.5, { size: 15, valign: 'middle' });

  // 3. Odeljak: kupci
  const section = async (num, title, sub, ic, col, sectionName, iconColor) => {
    pres.addSection({ title: sectionName });
    const sl = new S('Odeljak', sectionName);
    sl.text(num, 0.6, 1.7, 3, 0.9, { size: 54, bold: true, head: true, color: col });
    sl.text(title, 0.6, 2.7, 7.5, 1.0, { size: 44, bold: true, head: true, color: 'white' });
    sl.text(sub, 0.6, 3.85, 7.0, 1.4, { size: 18, color: 'mint' });
    await sl.icon(ic, 8.9, 1.55, 3.6, col, iconColor);
    return sl;
  };
  await section('01', 'Za kupce', 'Pretraga proizvoda, korpa, izbor između preuzimanja na tezgi i dostave, praćenje porudžbine.', 'MdShoppingBasket', 'carrot', 'Kupci');

  // 4. Pretraga
  s = new S('Sadrzaj - jedan ekran', 'Kupci', 'kupac');
  s.title('Pronađite proizvode');
  s.steps([
    { h: 'Filteri', d: 'Suzite izbor po pijaci, prodavcu i grupi proizvoda (voće, povrće…).' },
    { h: 'Pretraga po nazivu', d: 'Upišite deo naziva proizvoda. Kvačice nisu obavezne: „sljive“ nalazi i „šljive“.' },
    { h: 'Redosled', d: 'Dugme sa strelicama: po nazivu, po ceni ili najnovije prvo. Kod cene birate i jedinicu mere (kg, komad, veza…), jer se cene porede samo za istu jedinicu.' },
    { h: 'Meni ☰', d: 'Početna strana, vaše porudžbine, prijava, registracija i izbor jezika.' }
  ]);
  s.phone('c-home', RIGHT_X, PHONE_Y, PHONE_H, 'Početni ekran sa filterima, pretragom i dugmetom za redosled',
    [[1, 0.96, 0.148], [2, 0.62, 0.251], [3, 0.913, 0.312], [4, -0.11, 0.035]]);

  // 5. Dodavanje u korpu
  s = new S('Sadrzaj - jedan ekran', 'Kupci', 'kupac');
  s.title('Dodajte proizvode u korpu');
  s.steps([
    { h: 'Kartica proizvoda', d: 'Naziv, prodavac, pijaca i broj tezge, telefon prodavca i cena po jedinici mere.' },
    { h: 'Količina', d: 'Dugmićima − i + podesite koliko želite.' },
    { h: 'Dodaj', d: 'Proizvod ide u korpu. Ikonica korpe gore desno vodi do nje.' },
    { h: 'Više prodavaca u jednoj korpi', n: '✓', d: 'Možete kombinovati robu različitih prodavaca. Svaki prodavac dobija svoj deo porudžbine.' }
  ]);
  s.phone('c-home-products', RIGHT_X, PHONE_Y, PHONE_H, 'Kartice proizvoda sa količinom i dugmetom Dodaj',
    [[1, 0.40, 0.312], [2, 0.04, 0.459], [3, 0.40, 0.505]]);

  // 6. Korpa
  s = new S('Sadrzaj - jedan ekran', 'Kupci', 'kupac');
  s.title('Proverite korpu');
  s.steps([
    { h: 'Izmena količine', d: 'Dugmićima − i + menjate količinu; kada spustite na nulu, proizvod se uklanja.' },
    { h: 'Isprazni', d: 'Briše sve iz korpe (aplikacija prvo traži potvrdu).' },
    { h: 'Podaci za porudžbinu', d: 'Ime i prezime i telefon su obavezni. E-mail je opcioni: na njega stiže link za praćenje. Adresa je potrebna samo za dostavu kurirom.' }
  ]);
  s.phone('c-cart', RIGHT_X, PHONE_Y, PHONE_H, 'Korpa sa tri proizvoda i poljima za podatke',
    [[1, 0.955, 0.15], [2, 1.11, 0.035], [3, 0.75, 0.465]]);

  // 7. Način preuzimanja
  s = new S('Sadrzaj - jedan ekran', 'Kupci', 'kupac');
  s.title('Izaberite kako dobijate robu');
  const opt = async (y, n, ic, col, h, d) => {
    s.shape('round', 0.6, y, 7.9, 1.75, 'mint', { r: 0.08 });
    s.marker(n, 0.95, y + 0.4, 0.4);
    await s.icon(ic, 1.35, y + 0.18, 0.5, col);
    s.text(h, 2.05, y + 0.2, 6.2, 0.45, { size: 18, bold: true, color: 'forest', valign: 'middle' });
    s.text(d, 0.95, y + 0.8, 7.3, 0.8, { size: 14 });
  };
  await opt(1.75, 1, 'MdStorefront', 'green', 'Rezerviši – preuzimam sam',
    'Robu preuzimate na tezgi i plaćate na licu mesta. Prodavac potvrđuje rezervaciju i javlja kada je roba spremna.');
  await opt(3.75, 2, 'MdDeliveryDining', 'sky', 'Naruči – Eko kurir',
    'Kurir donosi robu na vašu adresu, a plaćate mu pri dostavi. Cena dostave piše na dugmetu i plaća se posebno za svakog prodavca.');
  await s.icon('MdInfo', 0.6, 5.85, 0.45, 'carrot');
  s.text('Porudžbina se trenutno ne može otkazati u aplikaciji. Ako se predomislite, pozovite prodavca; broj je na kartici proizvoda.', 1.2, 5.8, 7.3, 0.6, { size: 14 });
  s.phone('c-cart-checkout', RIGHT_X, PHONE_Y, PHONE_H, 'Dugmad Rezerviši – preuzimam sam i Naruči – Eko kurir',
    [[1, 0.93, 0.585], [2, 0.93, 0.71]]);

  // 8. Praćenje
  s = new S('Sadrzaj - jedan ekran', 'Kupci', 'kupac');
  s.title('Pratite porudžbinu bez naloga');
  s.steps([
    { h: 'Link za praćenje', d: 'Posle porudžbine dobijate link. Sačuvajte ga ili pošaljite sebi dugmetom „Podeli / kopiraj link“.' },
    { h: 'Status uživo', d: 'Promene stižu same, uz zvučni signal: potvrda prodavca, kurir na putu, isporuka.' },
    { h: 'Iznos za plaćanje', d: 'Roba i dostava zajedno; toliko plaćate prodavcu ili kuriru.' },
    { h: 'Uvek pri ruci', n: '✓', d: 'Telefon pamti vaše porudžbine: u meniju ☰ su pod „Moje porudžbine“. Link važi 30 dana od poslednje promene.' }
  ]);
  s.phone('c-track', RIGHT_X, PHONE_Y, PHONE_H, 'Stranica za praćenje porudžbine gosta',
    [[1, 0.68, 0.206], [2, 0.88, 0.444], [3, 0.88, 0.643]]);

  // 9. Nalog kupca
  s = new S('Sadrzaj - dva ekrana', 'Kupci', 'kupac');
  s.title('Nalog kupca');
  s.steps([
    { h: 'Prijava i registracija', d: 'Nalog nije obavezan, ali sa njim vidite sve porudžbine na svakom uređaju.' },
    { h: 'Moje porudžbine', d: 'Spisak sa statusom, iznosom, načinom preuzimanja, porukom prodavca i imenom kurira.' },
    { h: 'Detalji', n: '→', d: 'Dodir na porudžbinu otvara detalje (sledeća strana).' }
  ], 0.6, 1.75, 5.2);
  s.phone('c-login', LEFT_X, PHONE_Y, PHONE_H, 'Ekran za prijavu', [[1, 0.5, 0.465]]);
  s.phone('c-my-orders', RIGHT_X, PHONE_Y, PHONE_H, 'Spisak porudžbina kupca', [[2, 0.85, 0.137]]);

  // 10. Detalji porudžbine
  s = new S('Sadrzaj - jedan ekran', 'Kupci', 'kupac');
  s.title('Detalji porudžbine');
  s.steps([
    { h: 'Status', d: 'Gde je porudžbina trenutno (značenje statusa je na sledećoj strani).' },
    { h: 'Poruka prodavca', d: 'Npr. kada možete doći po robu ili da li nečega nema.' },
    { h: 'Kurir', d: 'Kada kurir preuzme dostavu, vidite njegovo ime i telefon.' },
    { h: 'Za plaćanje', d: 'Roba + Eko kurir. Plaća se gotovinom pri preuzimanju.' }
  ]);
  s.phone('c-order-detail', RIGHT_X, PHONE_Y, PHONE_H, 'Detalji porudžbine sa porukom prodavca i kurirom',
    [[1, 0.75, 0.155], [2, 0.93, 0.346], [3, 0.93, 0.45], [4, 0.1, 0.862]]);

  // 11. Statusi
  s = new S('Sadrzaj - bez ekrana', 'Kupci', 'kupac');
  s.title('Šta znači status porudžbine');
  const statuses = [
    ['Na čekanju', 'sun', 'Prodavac još nije odgovorio. Obično odgovara u roku od nekoliko minuta.'],
    ['Potvrđena', 'green', 'Prodavac je prihvatio porudžbinu i priprema robu.'],
    ['Spremna', 'sky', 'Roba je spakovana: dođite po nju na tezgu ili je kurir uskoro preuzima.'],
    ['U dostavi', 'carrot', 'Kurir je preuzeo robu i nosi je na vašu adresu.'],
    ['Završena', 'forest', 'Roba je preuzeta ili isporučena. Prijatno!'],
    ['Odbijena', 'tomato', 'Prodavac ne može da ispuni porudžbinu, razlog je obično u njegovoj poruci.']
  ];
  for (const [i, [label, col, d]] of statuses.entries()) {
    const x = 0.6 + (i % 2) * 6.15, y = 1.75 + Math.floor(i / 2) * 1.45;
    s.shape('round', x, y, 5.9, 1.15, 'mint', { r: 0.08 });
    s.shape('round', x + 0.3, y + 0.3, 1.75, 0.42, col, { r: 0.5 });
    s.text(label, x + 0.3, y + 0.3, 1.75, 0.42, { size: 14, bold: true, color: col === 'sun' ? 'ink' : 'white', align: 'center', valign: 'middle' });
    s.text(d, x + 2.3, y + 0.2, 3.4, 0.8, { size: 14 });
  }

  // 12. Odeljak: prodavci
  await section('02', 'Za prodavce', 'Prijem porudžbina, zvono za nove porudžbine, priprema robe i održavanje ponude i cena.', 'MdStorefront', 'mint', 'Prodavci', THEME.colors.dk2);

  // 13. Meni prodavca
  s = new S('Sadrzaj - jedan ekran', 'Prodavci', 'prodavac');
  s.title('Prvi koraci i meni prodavca');
  s.steps([
    { h: 'Pozivnica', d: 'Nalog otvara administrator. Na e-mail vam stiže pozivnica, a preko nje birate lozinku.' },
    { h: 'Jezik', d: 'Srpski, engleski ili ruski: izbor važi za ceo meni.' },
    { h: 'Porudžbine', d: 'Crveni broj pokazuje koliko porudžbina čeka vaš odgovor.' },
    { h: 'Profil, proizvodi, odjava', n: '✓', d: 'Ostale stavke vode do profila tezge, spiska proizvoda i dodavanja novog proizvoda.' }
  ]);
  s.phone('v-menu', RIGHT_X, PHONE_Y, PHONE_H, 'Meni prodavca', [[2, 0.62, 0.096], [3, 0.56, 0.432]]);

  // 14. Nova porudžbina
  s = new S('Sadrzaj - jedan ekran', 'Prodavci', 'prodavac');
  s.title('Nova porudžbina: potvrdite ili odbijte');
  s.steps([
    { h: 'Zvono', d: 'Dok imate porudžbinu na čekanju, telefon zvoni i vibrira na svakih nekoliko sekundi.' },
    { h: 'Šta je poručeno', d: 'Kupac i telefon, adresa, stavke, iznos i način: „Eko kurir“ ili „Rezervacija – lično preuzimanje“.' },
    { h: 'Potvrdi ili Odbij', d: 'Uz potvrdu možete napisati poruku kupcu, npr. „Spremno za 30 min, tezga 14“. Kod odbijanja je razlog obavezan: kupac ga dobija u aplikaciji i e-mailom. Zvono staje kada odgovorite na sve.' }
  ]);
  s.phone('v-orders', RIGHT_X, PHONE_Y, PHONE_H, 'Porudžbina na čekanju sa dugmadima Potvrdi i Odbij',
    [[2, 1.11, 0.35], [3, 0.65, 0.478]]);

  // 15. Priprema i predaja
  s = new S('Sadrzaj - jedan ekran', 'Prodavci', 'prodavac');
  s.title('Pripremite i predajte robu');
  s.steps([
    { h: 'Spremno', d: 'Kada je roba spakovana, pritisnite „Spremno“. Kupac, a kod dostave i kurir, dobijaju obaveštenje.' },
    { h: 'Rezervacija', d: 'Kada kupac dođe na tezgu i preuzme robu, pritisnite „Kupac preuzeo“.' },
    { h: 'Eko kurir', d: 'Vidite ime i telefon kurira koji dolazi po robu. Porudžbinu završava kurir posle isporuke.' }
  ]);
  s.phone('v-orders-more', RIGHT_X, PHONE_Y, PHONE_H, 'Potvrđene porudžbine sa dugmetom Spremno i podacima kurira',
    [[1, 0.42, 0.314], [3, 0.93, 0.677]]);

  // 16. Proizvodi
  s = new S('Sadrzaj - dva ekrana', 'Prodavci', 'prodavac');
  s.title('Ponuda i cene');
  s.steps([
    { h: 'Dostupno da/ne', d: 'Prekidač skida proizvod iz ponude kada nestane robe i vraća ga kada je ponovo imate.' },
    { h: 'Novi proizvod', d: 'Dugme + dodaje proizvod.' },
    { h: 'Izmena', d: 'Dodir na proizvod: naziv, cena, jedinica, grupa, klasa, napomena i slika. Slika sa telefona se sama smanjuje.' }
  ], 0.6, 1.75, 5.2);
  s.phone('v-products', LEFT_X, PHONE_Y, PHONE_H, 'Spisak proizvoda prodavca', [[1, 0.77, 0.117], [2, 0.72, 0.955]]);
  s.phone('v-product-form', RIGHT_X, PHONE_Y, PHONE_H, 'Izmena proizvoda', [[3, 0.85, 0.128]]);

  // 17. Profil tezge
  s = new S('Sadrzaj - jedan ekran', 'Prodavci', 'prodavac');
  s.title('Profil tezge');
  s.steps([
    { h: 'Osnovni podaci', d: 'Naziv, pijaca i broj tezge: tako vas kupci pronalaze u filterima.' },
    { h: 'Opis i telefoni', d: 'Kratak opis ponude i jedan ili više brojeva telefona koje kupci vide.' },
    { h: 'Promena lozinke', d: 'Unesite trenutnu i dva puta novu lozinku.' }
  ]);
  s.phone('v-profile', RIGHT_X, PHONE_Y, PHONE_H, 'Profil tezge sa promenom lozinke',
    [[1, 0.95, 0.125], [2, 0.93, 0.32], [3, 0.93, 0.62]]);

  // 18. Odeljak: kuriri
  await section('03', 'Za Eko kurire', 'Smena, zvono za nove dostave, prihvatanje ili odbijanje i isporuka korak po korak.', 'MdDeliveryDining', 'sky', 'Kuriri');

  // 19. Nove dostave
  s = new S('Sadrzaj - jedan ekran', 'Kuriri', 'kurir');
  s.title('Smena i nove dostave');
  s.steps([
    { h: 'Na smeni', d: 'Uključite prekidač da biste dobijali dostave. Van smene vam ne zvoni.' },
    { h: 'Zvono', d: 'Dok postoji dostava na koju niste odgovorili, telefon zvoni i vibrira.' },
    { h: 'Odakle i kuda', d: 'Prodavac, pijaca i tezga, adresa dostave, vrednost robe i vaša usluga. Ime i telefon kupca vidite tek kada prihvatite.' },
    { h: 'Odbij ili Prihvati', d: 'Dostavu dobija prvi kurir koji je prihvati. „Odbij“ je sklanja samo sa vašeg spiska.' }
  ]);
  s.phone('k-available', RIGHT_X, PHONE_Y, PHONE_H, 'Nova dostava sa dugmadima Odbij i Prihvati',
    [[1, 0.76, 0.17], [3, 0.9, 0.36], [4, 0.36, 0.565]]);

  // 20. Dostava
  s = new S('Sadrzaj - jedan ekran', 'Kuriri', 'kurir');
  s.title('Dostava korak po korak');
  s.steps([
    { h: 'Moje dostave', d: 'Prihvaćene dostave su na kartici „Moje“, sa imenom i telefonom kupca.' },
    { h: 'Adresa', d: 'Dodir na adresu otvara Google mape.' },
    { h: 'Preuzeo sam robu', d: 'Kada prodavac potvrdi porudžbinu i preda vam robu.' },
    { h: 'Isporučeno', n: '4', d: 'Posle predaje robe i naplate: aplikacija vas pita da li ste naplatili tačan iznos.' }
  ]);
  s.phone('k-mine', RIGHT_X, PHONE_Y, PHONE_H, 'Prihvaćena dostava sa dugmetom Preuzeo sam robu',
    [[1, 0.93, 0.1], [2, 0.82, 0.45], [3, 0.93, 0.723]]);

  // 21. Profil kurira
  s = new S('Sadrzaj - jedan ekran', 'Kuriri', 'kurir');
  s.title('Podaci kurira');
  s.steps([
    { h: 'Ime i telefon', d: 'Prodavac i kupac ih vide kada prihvatite dostavu.' },
    { h: 'Prevozno sredstvo', d: 'Bicikl, skuter, automobil… i registarska oznaka, ako je ima.' },
    { h: 'Promena lozinke', d: 'Unesite trenutnu i dva puta novu lozinku.' }
  ]);
  s.phone('k-profile', RIGHT_X, PHONE_Y, PHONE_H, 'Podaci kurira i promena lozinke',
    [[1, 0.93, 0.29], [2, 0.93, 0.38], [3, 0.93, 0.64]]);

  // 22. Pomoć
  pres.addSection({ title: 'Pomoć' });
  s = new S('Sadrzaj - jedan ekran', 'Pomoć');
  s.title('Pomoć i saveti');
  const tip = async (y, ic, col, h, d) => {
    await s.icon(ic, 0.6, y, 0.6, col);
    s.text(h, 1.4, y - 0.02, 7.1, 0.34, { size: 17, bold: true, color: 'forest' });
    s.text(d, 1.4, y + 0.36, 7.1, 0.8, { size: 14 });
  };
  await tip(1.75, 'MdSupportAgent', 'green', 'Tehnička podrška', 'U meniju ili podnožju strane: „Pošalji pitanje“. Odgovor stiže na e-mail koji navedete.');
  await tip(3.05, 'MdNotificationsActive', 'carrot', 'Zvuk i obaveštenja', 'Kada aplikacija zatraži dozvolu za obaveštenja, dozvolite je. Dok radite, ne utišavajte telefon.');
  await tip(4.35, 'MdPhoneAndroid', 'sky', 'Prodavci i kuriri: aplikacija otvorena', 'Dok ste na tezgi ili na smeni, držite aplikaciju otvorenu: tada zvono sigurno radi.');
  await tip(5.65, 'MdLock', 'forest', 'Lozinka', 'Najmanje 8 znakova; ne delite je sa drugima. Posle 10 pogrešnih pokušaja nalog se zaključava na 15 minuta.');
  s.phone('c-support', RIGHT_X, PHONE_Y, PHONE_H, 'Forma za tehničku podršku');

  await pres.writeFile({ fileName: OUT });
  await applyTheme(OUT, THEME);

  // HTML pregled
  const dir = path.join(__dirname, 'preview');
  fs.mkdirSync(dir, { recursive: true });
  previews.forEach((p, i) => {
    fs.writeFileSync(path.join(dir, `slide-${String(i + 1).padStart(2, '0')}.html`),
      `<!doctype html><html><head><meta charset="utf-8"><style>body{margin:0}#s{position:relative;width:${W * 96}px;height:${H * 96}px;overflow:hidden;background:#${p.bg}}#s div{box-sizing:border-box;overflow:visible}</style></head><body><div id="s">${p.html.join('')}</div></body></html>`);
  });
  console.log('written', OUT, previews.length, 'slides');
}

build().catch(e => { console.error(e); process.exit(1); });
