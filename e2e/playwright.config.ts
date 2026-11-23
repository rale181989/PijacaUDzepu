import { defineConfig, devices } from '@playwright/test';

// E2E testovi rade nad pravim API-jem (Development, demo podaci) i klijentom (ng serve).
// - Lokalno: ako API (:5000) i klijent (E2E_BASE_URL, podrazumevano :4200) već rade, koriste se oni.
// - CI: Playwright ih sam pokreće (webServer), nad praznom bazom iz DATABASE_URL.
const baseURL = process.env.E2E_BASE_URL ?? 'http://localhost:4200';
const apiURL = process.env.E2E_API_URL ?? 'http://localhost:5000';

export default defineConfig({
  testDir: './tests',
  // Testovi dele bazu i demo naloge (prodavac, kurir), pa idu jedan po jedan
  workers: 1,
  fullyParallel: false,
  timeout: 90_000,
  expect: { timeout: 15_000 },
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    ...devices['Pixel 7'],
    baseURL,
    locale: 'sr-Latn-RS',
    timezoneId: 'Europe/Belgrade',
    // Lokalno instaliran Chrome (E2E_CHANNEL=chrome) ili Playwright-ov Chromium (CI)
    channel: process.env.E2E_CHANNEL || undefined,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  metadata: { apiURL },
  webServer: [
    {
      command: 'dotnet run --project ../PijacaUDzepu.API/PijacaUDzepu.API --no-launch-profile',
      url: `${apiURL}/api/auth/health`,
      reuseExistingServer: !process.env.CI,
      timeout: 180_000,
      env: {
        ...(process.env as Record<string, string>),
        ASPNETCORE_ENVIRONMENT: 'Development',
        ASPNETCORE_URLS: 'http://localhost:5000',
      },
    },
    {
      command: 'npm start --prefix ../PijacaUDzepu.Client -- --port 4200',
      url: baseURL,
      reuseExistingServer: !process.env.CI,
      timeout: 240_000,
    },
  ],
});
