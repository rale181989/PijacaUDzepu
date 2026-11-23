import { expect, test } from '@playwright/test';
import { addToCart, alertButton, apiAs, button, login, newPhone, openVendor, productOf, visible } from './helpers';

// Lokacija tezge: prodavac na tezgi pritisne "Ovde sam" (GPS telefona), a kupac sa rezervacijom vidi tezgu na mapi.

test('prodavac postavi tačku tezge, kupac sa rezervacijom vidi mapu i "Do tezge"', async ({ browser }) => {
  const vendor = await openVendor('marko');
  const markets = await (await (await apiAs(await login('marko'))).get('/api/markets')).json() as { id: number; latitude: number; longitude: number }[];
  const market = markets.find(m => m.id === vendor.marketId)!;
  test.skip(!market?.latitude, 'Pijaca nema centar na mapi');

  // GPS ~20 m od centra pijace, precizan (±8 m): tačka se čuva odmah
  const gps = { latitude: market.latitude + 0.0002, longitude: market.longitude, accuracy: 8 };
  const prodavac = await newPhone(browser, await login('marko'));
  await prodavac.context.grantPermissions(['geolocation']);
  await prodavac.context.setGeolocation(gps);
  await prodavac.page.goto('/vendor-profile');
  await visible(prodavac.page.getByText('Lokacija tezge na mapi')).first().scrollIntoViewIfNeeded();
  await button(prodavac.page, 'Ovde sam').click();
  await expect(visible(prodavac.page.getByText(/Tačka je sačuvana \(GPS ± 8 m\)/)).first()).toBeVisible();

  // Kupac rezerviše i na detalju porudžbine vidi mapu tezge i navigaciju po koordinatama
  const product = await productOf(vendor.id);
  const kupac = await newPhone(browser, await login('kupac'), { market: String(vendor.marketId) });
  await addToCart(kupac.page, product);
  await kupac.page.goto('/cart');
  await button(kupac.page, 'Rezerviši – preuzimam sam').click();
  const created = kupac.page.waitForResponse(r => r.url().endsWith('/api/orders') && r.request().method() === 'POST');
  await alertButton(kupac.page, 'Rezerviši – preuzimam sam');
  const id = (await (await created).json() as { id: number }[])[0].id;

  await kupac.page.goto(`/order-detail/${id}`);
  await expect(visible(kupac.page.getByText('Prodavac je označio svoju tezgu na mapi')).first()).toBeVisible();
  await expect(visible(kupac.page.locator('app-stall-map .leaflet-container')).first()).toBeVisible();
  const doTezge = button(kupac.page, 'Do tezge');
  await expect(doTezge).toHaveJSProperty('href', `https://www.google.com/maps/dir/?api=1&destination=${gps.latitude.toFixed(6)},${gps.longitude.toFixed(6)}&travelmode=walking`);

  // Zatvaranje: prodavac odbije probnu rezervaciju
  const ctx = await apiAs(await login('marko'));
  await ctx.put(`/api/orders/${id}/status`, { data: { status: 'Rejected', message: 'E2E test' } });
  for (const p of [prodavac, kupac]) await p.context.close();
});
