import { expect, test } from '@playwright/test';
import { addToCart, actionSheetButton, alertButton, button, card, courierOnShift, login, newPhone, openVendor, productOf, visible } from './helpers';

// Glavni tokovi kupovine: gost rezerviše, kupac poruči kurira do kraja (prodavac → kurir → isporuka), prodavac odbije.

test('gost bira pijacu, rezerviše robu i dobija stranicu za praćenje', async ({ browser }) => {
  const vendor = await openVendor('marko');
  const product = await productOf(vendor.id);
  const { context, page } = await newPhone(browser);

  // Prvi put: bira se pijaca
  await page.goto('/home');
  await card(page, vendor.marketName).click();
  await addToCart(page, product);

  await page.goto('/cart');
  await expect(visible(page.locator('ion-item')).filter({ hasText: product.name }).first()).toBeVisible();
  await visible(page.getByLabel('Ime i prezime')).first().fill('Gost E2E');
  await visible(page.getByLabel('Telefon')).first().fill('0641112233');
  await button(page, 'Rezerviši – preuzimam sam').click();
  await alertButton(page, 'Rezerviši – preuzimam sam');

  await expect(page).toHaveURL(/\/prati\/[^/?]+\?nova=1/);
  await expect(visible(page.getByText('Hvala! Porudžbina je primljena.')).first()).toBeVisible();
  await expect(card(page, vendor.name)).toContainText('Na čekanju');
  await context.close();
});

test('kupac poruči kurira: prodavac potvrdi, kurir prihvati, preuzme i isporuči', async ({ browser }) => {
  const vendor = await openVendor('marko');
  const product = await productOf(vendor.id);
  await courierOnShift();

  // Kupac poručuje sa dostavom (telefon i adresa su iz naloga)
  const kupac = await newPhone(browser, await login('kupac'), { market: String(vendor.marketId) });
  await addToCart(kupac.page, product);
  await kupac.page.goto('/cart');
  await button(kupac.page, 'Naruči – kurir').click();
  const created = kupac.page.waitForResponse(r => r.url().endsWith('/api/orders') && r.request().method() === 'POST');
  await alertButton(kupac.page, /^Naruči$/);
  const orders = await (await created).json() as { id: number }[];
  const id = orders[0].id;
  await expect(kupac.page).toHaveURL(/\/my-orders/);

  // Prodavac potvrđuje
  const prodavac = await newPhone(browser, await login('marko'));
  await prodavac.page.goto('/vendor-orders');
  const vendorCard = card(prodavac.page, `#${id} —`);
  await button(vendorCard, 'Potvrdi').click();
  await alertButton(prodavac.page, 'Pošalji');
  await expect(vendorCard).toContainText('Čeka se kurir');

  // Kurir: "Nove" → Prihvati → "Moje" → Preuzeo sam robu → Isporučeno
  const kurir = await newPhone(browser, await login('kurir'));
  await kurir.page.goto('/courier-orders');
  const offer = card(kurir.page, `#${id}`);
  await expect(offer).toBeVisible();
  await button(offer, /^\s*Prihvati\s*$/).click();
  await button(kurir.page, /Moje/).click();
  const mine = card(kurir.page, `#${id}`);
  await button(mine, 'Preuzeo sam robu').click();
  await alertButton(kurir.page, 'Preuzeo sam');
  await expect(mine).toContainText('U dostavi');
  await button(mine, /^\s*Isporučeno\s*$/).click();
  await alertButton(kurir.page, 'Isporučeno');

  // Kupac vidi da je porudžbina završena
  await kupac.page.goto(`/order-detail/${id}`);
  await expect(visible(kupac.page.getByText('Završena')).first()).toBeVisible();

  for (const p of [kupac, prodavac, kurir]) await p.context.close();
});

test('prodavac odbije rezervaciju sa razlogom, kupac vidi crvenu poruku', async ({ browser }) => {
  const vendor = await openVendor('marko');
  const product = await productOf(vendor.id);

  const kupac = await newPhone(browser, await login('kupac'), { market: String(vendor.marketId) });
  await addToCart(kupac.page, product);
  await kupac.page.goto('/cart');
  await button(kupac.page, 'Rezerviši – preuzimam sam').click();
  const created = kupac.page.waitForResponse(r => r.url().endsWith('/api/orders') && r.request().method() === 'POST');
  await alertButton(kupac.page, 'Rezerviši – preuzimam sam');
  const id = (await (await created).json() as { id: number }[])[0].id;

  const prodavac = await newPhone(browser, await login('marko'));
  await prodavac.page.goto('/vendor-orders');
  await button(card(prodavac.page, `#${id} —`), 'Odbij').click();
  await actionSheetButton(prodavac.page, 'Nestalo je robe');

  await kupac.page.goto(`/order-detail/${id}`);
  await expect(visible(kupac.page.getByText('Prodavac je odbio porudžbinu')).first()).toBeVisible();
  await expect(visible(kupac.page.getByText('Nestalo je robe')).first()).toBeVisible();

  for (const p of [kupac, prodavac]) await p.context.close();
});
