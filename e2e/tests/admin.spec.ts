import { expect, test } from '@playwright/test';
import { alertButton, apiAs, button, card, login, newPhone, openVendor, productOf, visible } from './helpers';

// Administrator zatvara porudžbinu koja je ostala otvorena ("Otvorene porudžbine" → Završi).

test('administrator zatvara zaboravljenu porudžbinu', async ({ browser }) => {
  const vendor = await openVendor('marko');
  const product = await productOf(vendor.id);
  const kupacApi = await apiAs(await login('kupac'));
  const res = await kupacApi.post('/api/orders', {
    data: { items: [{ productId: product.id, quantity: 1 }], fulfillment: 'Pickup', contactPhone: '0641112233' }
  });
  expect(res.ok(), await res.text()).toBeTruthy();
  const id = (await res.json() as { id: number }[])[0].id;

  const admin = await newPhone(browser, await login('admin'));
  await admin.page.goto('/admin-orders');
  // Najstarije su prve i lista je po stranama: tražimo porudžbinu po broju i listamo dalje dok se ne pojavi
  await expect(visible(admin.page.locator('ion-card')).first()).toBeVisible();
  const orderCard = card(admin.page, `#${id} ·`);
  for (let i = 0; i < 20 && !(await orderCard.isVisible()); i++) {
    // Pager: [Prethodna] [Sledeća]; Ionic prebacuje aria-label na unutrašnje dugme, a "disabled" ostaje atribut
    const next = admin.page.locator('app-pager ion-button').filter({ visible: true }).last();
    if (!(await next.isVisible()) || (await next.getAttribute('disabled')) !== null) break;
    await next.click();
    await admin.page.waitForTimeout(500);
  }
  await expect(orderCard).toBeVisible();
  await button(orderCard, 'Završi').click();
  await alertButton(admin.page, 'Završi');
  await expect(card(admin.page, `#${id} ·`)).toHaveCount(0);

  const order = await (await kupacApi.get(`/api/orders/${id}`)).json();
  expect(order.status).toBe('Completed');
  await admin.context.close();
});
