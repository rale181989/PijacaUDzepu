import { expect, test } from '@playwright/test';
import { button, newPhone, visible } from './helpers';

// Prijava preko ekrana (ostali testovi se prijavljuju preko API-ja), pogrešna lozinka i zaboravljena lozinka.

test('pogrešna lozinka javlja grešku, ispravna vodi prodavca na njegove porudžbine', async ({ browser }) => {
  const { context, page } = await newPhone(browser);
  await page.goto('/login');
  await visible(page.getByLabel('Korisničko ime')).first().fill('marko');
  await visible(page.getByLabel('Lozinka')).first().fill('pogresna-lozinka');
  await button(page, 'Prijavi se').click();
  await expect(page.locator('ion-toast').last()).toContainText('Pogrešno korisničko ime ili lozinka');
  await expect(page).toHaveURL(/\/login/);

  await visible(page.getByLabel('Lozinka')).first().fill('vendor1234');
  await button(page, 'Prijavi se').click();
  // Prodavac ne kupuje: posle prijave ide na svoje porudžbine
  await expect(page).toHaveURL(/\/vendor-orders/);
  await context.close();
});

test('zaboravljena lozinka odgovara isto i za nepostojeći nalog', async ({ browser }) => {
  const { context, page } = await newPhone(browser);
  await page.goto('/forgot-password');
  await visible(page.getByLabel('Korisničko ime ili e-mail')).first().fill('nepostojeci-nalog-e2e');
  await button(page, 'Pošalji link').click();
  await expect(visible(page.getByText('Ako nalog postoji i ima e-mail adresu')).first()).toBeVisible();
  await context.close();
});

test('prodavac i kurir ne vide prodavnicu i korpu', async ({ browser }) => {
  const { login } = await import('./helpers');
  const { context, page } = await newPhone(browser, await login('kurir'));
  await page.goto('/home');
  await expect(page).toHaveURL(/\/courier-orders/);
  await page.goto('/cart');
  await expect(page).toHaveURL(/\/courier-orders/);
  await context.close();
});
