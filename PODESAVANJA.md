# Podešavanja koja treba aktivirati

Spisak stvari koje aplikacija podržava, ali ne rade dok se ne podese.
Kad nešto završiš, štikliraj ga ovde (`[x]`).

Env varijable na Render-u se podešavaju u **Render Dashboard → servis → Environment**,
a posle izmene treba pokrenuti **Manual Deploy** (ili sačekati sledeći deploy).
U nazivima env varijabli dvostruka donja crta `__` znači „pod-podešavanje”
(npr. `Email__SmtpUser` = `Email:SmtpUser` iz `appsettings.json`).

---

## 1. Slanje e-mailova (Brevo) – VAŽNO

- [ ] Napravljen Brevo nalog
- [ ] Potvrđena adresa pošiljaoca
- [ ] Env varijable dodate na Render

**Bez ovoga ne rade:**
- ❌ pozivnice prodavcima (Admin → Prodavci → pozovi) – slanje pada sa greškom
- ❌ kopija pitanja za podršku na zivkovic_kac@live.com (pitanja i dalje stižu u admin panel „Podrška”)
- ❌ e-mail sa linkom za praćenje za goste (link se i dalje vidi na ekranu posle kupovine)

Sve ostalo (porudžbine, kuriri, obaveštenja u aplikaciji) radi i bez toga.

### Uputstvo
1. Napravi besplatan nalog na <https://www.brevo.com> (besplatno do 300 e-mailova dnevno).
2. Idi na **Senders, Domains & Dedicated IPs → Senders** i dodaj/potvrdi adresu pošiljaoca.
   - Aplikacija podrazumevano šalje sa `noreply@pijacaudzepu.rs`. Ako taj domen još nije tvoj,
     potvrdi adresu koju imaš (npr. svoj Gmail ili adresu na svom domenu) i postavi je kao `Email__SenderEmail`.
   - Brevo odbija poruke od nepotvrđenog pošiljaoca.
3. Idi na **SMTP & API → SMTP**:
   - „Login” je vrednost za `Email__SmtpUser`
   - klikni **Generate a new SMTP key** – ključ je vrednost za `Email__SmtpPass` (vidi se samo jednom, sačuvaj ga)
4. Na Render-u dodaj:

   | Env varijabla | Vrednost |
   |---|---|
   | `Email__SmtpUser` | Login iz Brevo-a |
   | `Email__SmtpPass` | SMTP ključ iz Brevo-a |
   | `Email__SenderEmail` | potvrđena adresa pošiljaoca (samo ako nije `noreply@pijacaudzepu.rs`) |
   | `Support__Email` | adresa na koju stižu pitanja (podrazumevano `zivkovic_kac@live.com`, ne mora da se podešava) |

5. Proba: pošalji pitanje preko stranice „Tehnička podrška” i poruči nešto kao gost sa svojim e-mailom.
   Ako e-mail ne stigne, pogledaj Render logove – poruka `Email nije podešen` znači da varijable nisu učitane.

> Lokalno: iste vrednosti mogu da se upišu u `PijacaUDzepu.API/PijacaUDzepu.API/appsettings.json`
> pod `Email` (`SmtpUser`, `SmtpPass`). **Ne commit-uj ih u git.**

---

## 2. Objava backenda na Render

- [ ] Današnje izmene commit-ovane i push-ovane na `main`

Na Render-u trenutno radi stari kod: nema kurira, rezervacija, praćenja, podrške,
a **blokira mobilnu aplikaciju** (CORS). Dok se ne objavi, APK prikazuje prazan ekran.

---

## 3. Push obaveštenja na telefonu (Firebase)

- [ ] Napravljen Firebase projekat sa Android aplikacijom `rs.pijacaudzepu.app`
- [ ] `google-services.json` dodat u `PijacaUDzepu.Client/android/app/`
- [ ] `pushEnabled: true` u `PijacaUDzepu.Client/src/environments/environment.mobile.ts`
- [ ] Env varijabla `FIREBASE_CREDENTIALS_JSON` na Render-u

Bez ovoga obaveštenja stižu samo dok je aplikacija otvorena.

### Uputstvo
1. <https://console.firebase.google.com> → **Add project**.
2. U projektu **Add app → Android**, package name: `rs.pijacaudzepu.app`.
3. Preuzmi `google-services.json` i stavi ga u `PijacaUDzepu.Client/android/app/`.
4. U `environment.mobile.ts` promeni `pushEnabled: false` u `pushEnabled: true`.
   **Ne uključuj pre koraka 3** – bez tog fajla aplikacija se ruši pri prijavi.
5. Firebase → **Project settings → Service accounts → Generate new private key**.
   Ceo sadržaj preuzetog JSON fajla upiši na Render kao `FIREBASE_CREDENTIALS_JSON`.
6. `npm run build:android` u `PijacaUDzepu.Client`, pa napravi novi APK.

---

## 4. Cena Eko kurira

- [ ] Upisana dogovorena cena

Trenutno je **300 din** po porudžbini (po prodavcu). Na Render-u:

| Env varijabla | Vrednost |
|---|---|
| `Delivery__CourierFee` | npr. `350` |

Aplikacija čita cenu sa servera, pa za ovo ne treba novi APK.

---

## 5. Podaci o firmi u pravnim tekstovima

- [ ] Popunjeni podaci

U `PijacaUDzepu.Client/src/app/legal/legal-content.ts`, objekat `LEGAL_INFO`:
naziv firme, adresa, PIB, matični broj, kontakt e-mail, telefon i **naziv kurirske službe**
(sada stoje kao `[...]`). Uslove korišćenja i politiku privatnosti bi pre objave trebalo da pogleda pravnik.

---

## 6. Lokalni server u APK-u (testiranje na telefonu)

- [ ] Odlučeno kako

APK može da radi i sa API-jem na računaru (ekran za prijavu → „Server: Promeni” → `http://IP-računara:5000`),
ali Android 9+ blokira `http://`. Opcije:
1. **Dozvola samo u debug APK-u** (preporuka) – fajl `android/app/src/debug/AndroidManifest.xml`
   sa `android:usesCleartextTraffic="true"`; APK za objavu ostaje zaštićen.
2. HTTPS tunel do računara (Cloudflare Tunnel / ngrok) – bez izmena u Androidu.

Ako se izabere opcija 2, iz `PijacaUDzepu.Client/capacitor.config.ts` ukloniti `cleartext` i `allowMixedContent`.
