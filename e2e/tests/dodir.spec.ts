import { expect, test } from '@playwright/test';
import { apiAs, button, card, courierOnShift, login, newPhone, openVendor, productOf } from './helpers';

// Jedan DODIR (touch, kao na telefonu, ne klik mišem) na "Potvrdi" i "Prihvati" mora da proradi iz prvog puta
test('jedan dodir otvara potvrdu prodavca i prihvata dostavu kod kurira', async ({ browser }) => {
  const vendor = await openVendor('marko');
  const product = await productOf(vendor.id);
  await courierOnShift();
  const kupacApi = await apiAs(await login('kupac'));
  const mk = async (fulfillment: string) => (await (await kupacApi.post('/api/orders', { data: { items: [{ productId: product.id, quantity: 1 }], fulfillment, contactPhone: '0641112233', deliveryAddress: 'Dunavska 5, Novi Sad' } })).json())[0].id as number;

  // Prodavac: jedan dodir na "Potvrdi" treba da otvori dijalog
  const pickupId = await mk('Pickup');
  const prodavac = await newPhone(browser, await login('marko'));
  await prodavac.page.goto('/vendor-orders');
  const pCard = card(prodavac.page, `#${pickupId} —`);
  await pCard.scrollIntoViewIfNeeded();
  await prodavac.page.waitForTimeout(1500);
  await button(pCard, 'Potvrdi').tap();
  await prodavac.page.waitForTimeout(1500);
  await expect(prodavac.page.locator('ion-alert')).toHaveCount(1);

  // Kurir: jedan dodir na "Prihvati" treba da pošalje zahtev
  const courierId = await mk('Courier');
  const mApi = await apiAs(await login('marko'));
  await mApi.put(`/api/orders/${courierId}/status`, { data: { status: 'Confirmed' } });
  const kurir = await newPhone(browser, await login('kurir'));
  const requests: string[] = [];
  kurir.page.on('request', r => { if (r.url().includes('/accept')) requests.push(r.url()); });
  await kurir.page.goto('/courier-orders');
  const kCard = card(kurir.page, `#${courierId}`);
  await kCard.scrollIntoViewIfNeeded();
  await kurir.page.waitForTimeout(1500);
  await button(kCard, /^\s*Prihvati\s*$/).tap();
  await kurir.page.waitForTimeout(2000);
  expect(requests).toHaveLength(1);

  await mApi.put(`/api/orders/${pickupId}/status`, { data: { status: 'Rejected', message: 'E2E' } }).catch(() => {});
  const admin = await apiAs(await login('admin'));
  await admin.post(`/api/orders/${courierId}/admin-complete`).catch(() => {});
  for (const p of [prodavac, kurir]) await p.context.close();
});
