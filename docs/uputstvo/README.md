# Korisničko uputstvo (PowerPoint)

Uputstvo za kupce, prodavce i kurire, sa snimcima ekrana aplikacije. Pravi se iz koda:

| Fajl | Šta radi |
|---|---|
| `setup.js` | pravi demo porudžbine u **lokalnoj** bazi (kupac, gost, prodavac Marko, kurir na smeni) |
| `shots.js` | snima ekrane telefona (390×844) iz lokalne aplikacije u `shots/` |
| `deck.js` | pravi `Pijaca-u-Dzepu-uputstvo.pptx` i HTML pregled slajdova u `preview/` |
| `render.js` | pretvara HTML pregled u PNG i javlja tekst koji izlazi iz okvira |
| `apply_theme.js` | upisuje boje i fontove teme u .pptx (pptxgenjs to ne radi sam) |

## Pokretanje
```
npm install
# API (http://localhost:5000) i klijent (npx ng serve --port 8100) moraju da rade lokalno
node setup.js                                   # jednom, za demo porudžbine
APP=http://localhost:8100 node shots.js         # svi ekrani; ili: node shots.js k-available v-orders
node deck.js                                    # -> Pijaca-u-Dzepu-uputstvo.pptx
node render.js                                  # provera izgleda: preview/slide-NN.png
```
Snimanje koristi instalirani Google Chrome (`C:/Program Files/Google/Chrome/Application/chrome.exe`).

## Kad se aplikacija promeni
Ažuriraj tekst slajda u `deck.js`, po potrebi ponovo snimi ekran (`shots.js <ime>`), napravi uputstvo i proveri
`preview/` - narandžasti brojevi (`markers` uz `phone(...)`) su položaji na slici (0–1), pa ih treba pomeriti ako se ekran promenio.
