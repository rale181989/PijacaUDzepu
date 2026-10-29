// Izbor servera u mobilnoj aplikaciji: isti APK radi sa Render-om (podrazumevano)
// ili sa lokalnim API-jem na računaru u istoj Wi-Fi mreži (npr. http://192.168.1.10:5000).
// Adresa se čita pre pokretanja aplikacije (u environment.mobile.ts), pa je koriste svi servisi.

export const DEFAULT_API_SERVER = 'https://pijacaudzepu.onrender.com';

const STORAGE_KEY = 'apiServer';

export function getApiServer(): string {
  try {
    return localStorage.getItem(STORAGE_KEY) || DEFAULT_API_SERVER;
  } catch {
    return DEFAULT_API_SERVER;
  }
}

// Vraća normalizovanu adresu ili null ako nije ispravna (mora http(s)://host[:port], bez putanje)
export function normalizeApiServer(value: string): string | null {
  try {
    const url = new URL(value.trim());
    if (url.protocol !== 'http:' && url.protocol !== 'https:') return null;
    return `${url.protocol}//${url.host}`;
  } catch {
    return null;
  }
}

// Posle promene aplikacija se ponovo učitava da bi svi servisi koristili novu adresu
// Prijava i korpa sa jednog servera ne važe na drugom, pa se brišu
export function setApiServer(server: string | null) {
  try {
    if (server && server !== DEFAULT_API_SERVER) localStorage.setItem(STORAGE_KEY, server);
    else localStorage.removeItem(STORAGE_KEY);
    ['token', 'user', 'cart'].forEach(key => localStorage.removeItem(key));
  } catch {}
  window.location.reload();
}
