import { APIRequestContext, Browser, BrowserContext, Locator, Page, expect, request } from '@playwright/test';

export const API = process.env.E2E_API_URL ?? 'http://localhost:5000';

// Demo nalozi (Development seed)
export const USERS = {
  kupac: { userName: 'kupac', password: 'kupac1234' },
  marko: { userName: 'marko', password: 'vendor1234' },
  jovana: { userName: 'jovana', password: 'vendor1234' },
  kurir: { userName: 'kurir1', password: 'kurir1234' },
  admin: { userName: 'admin', password: 'admin1234' },
} as const;
export type UserKey = keyof typeof USERS;

export interface Auth { token: string; user: unknown }
export interface Product { id: number; name: string; vendorId: number; vendorName: string; marketId: number; marketName: string; price: number; isAvailable: boolean; stockQuantity: number | null; vendorOpen?: boolean }

// Svaki "korisnik" dolazi sa svoje adrese: ograničenje prijava (10/min po adresi) ne sme da obori testove
let ipCounter = Math.floor(Math.random() * 200);
export const uniqueIp = () => `10.77.${Math.floor(Math.random() * 250)}.${(ipCounter++ % 250) + 1}`;

export async function api(): Promise<APIRequestContext> {
  return request.newContext({ baseURL: API, extraHTTPHeaders: { 'X-Forwarded-For': uniqueIp() } });
}

export async function login(user: UserKey): Promise<Auth> {
  const ctx = await api();
  const res = await ctx.post('/api/auth/login', { data: USERS[user] });
  expect(res.ok(), `prijava ${user}: ${res.status()} ${await res.text()}`).toBeTruthy();
  const auth = await res.json() as Auth;
  await ctx.dispose();
  return auth;
}

export async function apiAs(auth: Auth): Promise<APIRequestContext> {
  return request.newContext({ baseURL: API, extraHTTPHeaders: { Authorization: `Bearer ${auth.token}`, 'X-Forwarded-For': uniqueIp() } });
}

// Nov "telefon": prazan localStorage, sopstvena adresa, opciono prijavljen (token kao posle prijave)
export async function newPhone(browser: Browser, auth?: Auth, extra: Record<string, string> = {}): Promise<{ context: BrowserContext; page: Page }> {
  const context = await browser.newContext({ extraHTTPHeaders: { 'X-Forwarded-For': uniqueIp() } });
  await context.addInitScript(([a, ex]) => {
    if (sessionStorage.getItem('e2e-init')) return; // samo pri prvom učitavanju, posle radi kao prava aplikacija
    sessionStorage.setItem('e2e-init', '1');
    localStorage.clear();
    localStorage.setItem('howItWorksSeen', '1');
    for (const [k, v] of Object.entries(ex as Record<string, string>)) localStorage.setItem(k, v);
    if (a) {
      localStorage.setItem('token', (a as Auth).token);
      localStorage.setItem('user', JSON.stringify((a as Auth).user));
    }
  }, [auth ?? null, extra] as const);
  const page = await context.newPage();
  return { context, page };
}

// Ionic drži prethodne stranice u DOM-u (sakrivene): radimo samo sa vidljivim elementima
export const visible = (l: Locator) => l.filter({ visible: true });

export const button = (scope: Page | Locator, text: string | RegExp) =>
  visible(scope.locator('ion-button, ion-segment-button').filter({ hasText: text })).first();

export async function alertButton(page: Page, text: string | RegExp) {
  const b = page.locator('ion-alert button').filter({ hasText: text }).last();
  await expect(b).toBeVisible();
  await b.click();
}

export async function actionSheetButton(page: Page, text: string | RegExp) {
  const b = page.locator('ion-action-sheet button').filter({ hasText: text }).last();
  await expect(b).toBeVisible();
  await b.click();
}

export const card = (page: Page, text: string | RegExp) => visible(page.locator('ion-card').filter({ hasText: text })).first();

// Prodavac prima porudžbine: bez pauze, radnog vremena i neradnih dana (demo podaci između testova mogu biti menjani)
export async function openVendor(user: UserKey) {
  const ctx = await apiAs(await login(user));
  const res = await ctx.put('/api/vendors/my-vendor/availability', { data: { ordersPaused: false, openingHours: [], closedDates: [], earlyCloseToday: null } });
  expect(res.ok(), await res.text()).toBeTruthy();
  const vendor = await res.json();
  await ctx.dispose();
  return vendor as { id: number; name: string; marketId: number; marketName: string };
}

// Proizvod prodavca koji može da se poruči (dostupan, bez stanja ili sa dovoljno robe)
export async function productOf(vendorId: number): Promise<Product> {
  const ctx = await api();
  const res = await ctx.get('/api/products?take=100');
  const items = (await res.json()).items as Product[];
  await ctx.dispose();
  const p = items.find(i => i.vendorId === vendorId && i.isAvailable && (i.stockQuantity === null || i.stockQuantity >= 5));
  if (!p) throw new Error(`Prodavac ${vendorId} nema proizvod za poručivanje`);
  return p;
}

export async function courierOnShift() {
  const ctx = await apiAs(await login('kurir'));
  const res = await ctx.put('/api/couriers/me/shift', { data: { isOnShift: true } });
  expect(res.ok(), await res.text()).toBeTruthy();
  await ctx.dispose();
}

// Kupac na početnoj: pijaca je izabrana, nađe proizvod pretragom i doda ga u korpu
export async function addToCart(page: Page, product: Product) {
  await page.goto('/home');
  await visible(page.getByPlaceholder('Traži proizvod')).first().fill(product.name);
  // Isti naziv može imati više prodavaca: kartica mora imati i naziv i prodavca
  const productCard = visible(page.locator('ion-card').filter({ hasText: product.name }).filter({ hasText: product.vendorName })).first();
  await expect(productCard).toBeVisible();
  await button(productCard, 'Dodaj').click();
  await expect(page.locator('ion-toast').last()).toContainText('dodato u korpu');
}
