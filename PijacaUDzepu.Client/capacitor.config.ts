import type { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: 'rs.pijacaudzepu.app',
  appName: 'Pijaca u Džepu',
  webDir: 'www',
  server: {
    androidScheme: 'https',
    // Dozvoljava http:// ka lokalnom API-ju (izbor servera na ekranu za prijavu).
    // Render ide preko https-a; pre objave na Play prodavnici razmotriti isključivanje.
    cleartext: true
  },
  android: {
    // Aplikacija se učitava sa https://localhost, a lokalni API je http:// - bez ovoga WebView ga blokira
    allowMixedContent: true
  }
};

export default config;
