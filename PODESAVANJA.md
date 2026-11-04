# Podešavanja koja treba aktivirati

Spisak stvari koje aplikacija podržava, ali ne rade dok se ne podese.
Kad nešto završiš, štikliraj ga ovde (`[x]`).

Env varijable na Render-u se podešavaju u **Render Dashboard → servis → Environment**,
a posle izmene treba pokrenuti **Manual Deploy** (ili sačekati sledeći deploy).
U nazivima env varijabli dvostruka donja crta `__` znači „pod-podešavanje”
(npr. `Email__SmtpUser` = `Email:SmtpUser` iz `appsettings.json`).

---

## 0. Trenutno: DEMO faza

Aplikacija je za sada demo: test nalozi i demo prodavci/proizvodi ostaju, admin može da ostane sa `admin1234`.
Na Render-u je za to potrebna **samo jedna** varijabla:

- [ ] `DEMO_ACCOUNTS` = `true` dodat na Render (pre sledećeg deploy-a)

Bez nje server u produkciji **zaključava** test naloge koji imaju poznate lozinke (kupac, kurir1/2, marko… –
`kupac1234`, `kurir1234`, `vendor1234`) i prijava na njih javlja „Nalog je onemogućen”. Admin se ne zaključava.

Kako se dodaje: Render → servis → **Environment** → **Add Environment Variable** → Key `DEMO_ACCOUNTS`,
Value `true` → **Save Changes** (Render sam restartuje servis). Pri pokretanju server otključa test naloge.
Provera: u Render **Logs** posle restarta stoji `Test nalozi: uključeni (DEMO_ACCOUNTS=true)`;
ako piše `zaključani (DEMO_ACCOUNTS=nije postavljen)`, varijabla nije sačuvana na pravom servisu.
Prihvata se i `True`, `1`, `yes`, `da`.

`ADMIN_PASSWORD` u demo fazi nije obavezan. Bez njega admin radi sa `admin1234`, a u Render logu pri svakom
pokretanju stoji upozorenje – to je očekivano dok traje demo. Imaj na umu da je `admin1234` zapisan u README-u,
pa ko zna adresu aplikacije može da uđe kao admin; u demo podacima nema ničeg pravog, pa to sada nije problem.

Ostala zaštita radi i u demo fazi: lozinke najmanje 8 znakova, posle 10 pogrešnih pokušaja nalog je zaključan
15 minuta, najviše 10 pokušaja prijave u minuti sa jedne IP adrese.

---

## 0a. Prelazak u produkciju – kada javiš „ide u produkciju”

Redom, pre nego što aplikaciju dobiju pravi kupci i prodavci. Stavke označene sa 🤖 radi Claude u kodu
(javi mu kad kreće), ostalo se podešava na Render-u i u spoljnim servisima.

**Nalozi i demo podaci**
- [ ] `ADMIN_PASSWORD` na Render-u = jaka lozinka (najmanje 8 znakova, bar jedno malo slovo).
      Pri pokretanju server zameni `admin1234` ovom lozinkom; posle se prijavljuješ njom.
- [ ] **Obriši** `DEMO_ACCOUNTS` sa Render-a. Test nalozi se pri pokretanju sami zaključaju.
- [ ] 🤖 Uklanjanje demo prodavaca, tezgi, proizvoda i test porudžbina iz baze (četiri pijace ostaju).
      Uradi backup/granu baze na Neon-u pre toga (odeljak 9).
- [ ] 🤖 Iz README-a izbaciti demo lozinke.
- [ ] Pravi prodavci: Admin → Prodavci → napravi prodavca i pošalji pozivnicu (traži e-mail, odeljak 1).
- [ ] Pravi kuriri: Admin → Korisnici → „Dodaj kurira”, sa pravim lozinkama.

**Obavezni servisi**
- [ ] E-mail (Brevo) – odeljak 1. Bez njega ne rade pozivnice, zaboravljena lozinka i e-mail o odbijenoj porudžbini.
- [ ] Slike u Cloudflare R2 – odeljak 7. Bez toga slike prodavaca nestaju pri svakom deploy-u.
- [ ] Backup baze (Neon) – odeljak 9.

**Preporučeno**
- [ ] Push obaveštenja (Firebase) – odeljak 3 (🤖 uključuje `pushEnabled` i pravi novi APK kad je `google-services.json` tu).
- [ ] Praćenje grešaka (Sentry) – odeljak 8.
- [ ] Cena Eko kurira – odeljak 4.

**Pravno i objava**
- [ ] Podaci o firmi u pravnim tekstovima – odeljak 5 (🤖 upisuje kad dobije podatke).
- [ ] 🤖 APK za objavu: bez `http://` (odeljak 6), potpisan ključem za Google Play.

**Provera posle deploy-a**
- [ ] Prijava kao `admin` novom lozinkom radi, a `admin1234` više ne.
- [ ] Prijava kao `kupac` / `kupac1234` javlja „Nalog je onemogućen”.
- [ ] Probna porudžbina od početka do kraja (kupac → prodavac → kurir), pa je otkaži/odbij.

---

## 1. Slanje e-mailova (Brevo) – VAŽNO

- [ ] Napravljen Brevo nalog
- [ ] Potvrđena adresa pošiljaoca
- [ ] Env varijable dodate na Render

**Bez ovoga ne rade:**
- ❌ pozivnice prodavcima (Admin → Prodavci → pozovi) – slanje pada sa greškom
- ❌ kopija pitanja za podršku na zivkovic_kac@live.com (pitanja i dalje stižu u admin panel „Podrška”)
- ❌ e-mail sa linkom za praćenje za goste (link se i dalje vidi na ekranu posle kupovine)
- ❌ „Zaboravili ste lozinku?” – link za novu lozinku ne stiže (administrator i dalje može da postavi privremenu lozinku u Admin → Korisnici → 🔑)
- ❌ e-mail kupcu sa razlogom kad prodavac odbije porudžbinu

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

## 4a. Upozorenje za porudžbine koje čekaju (opciono)

Server sam proverava svakog minuta i javlja adminu (obaveštenje u aplikaciji + push) kada porudžbina
čeka prodavca duže od 15 minuta ili kurira duže od 20 minuta; prodavcu tada ponovo zazvoni.
Pragovi se menjaju na Render-u (bez novog APK-a):

| Env varijabla | Podrazumevano |
|---|---|
| `Alerts__PendingMinutes` | `15` |
| `Alerts__CourierMinutes` | `20` |

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
U politici privatnosti navedi i da korisnik može sam da obriše nalog (Moj nalog → Obriši nalog) i šta se tada
čuva (porudžbine bez ličnih podataka) – to traži i Google Play pri objavi.

---

## 6. Lokalni server u APK-u (testiranje na telefonu)

- [ ] Odlučeno kako

APK može da radi i sa API-jem na računaru (ekran za prijavu → „Server: Promeni” → `http://IP-računara:5000`),
ali Android 9+ blokira `http://`. Opcije:
1. **Dozvola samo u debug APK-u** (preporuka) – fajl `android/app/src/debug/AndroidManifest.xml`
   sa `android:usesCleartextTraffic="true"`; APK za objavu ostaje zaštićen.
2. HTTPS tunel do računara (Cloudflare Tunnel / ngrok) – bez izmena u Androidu.

Ako se izabere opcija 2, iz `PijacaUDzepu.Client/capacitor.config.ts` ukloniti `cleartext` i `allowMixedContent`.

---

## 7. Slike u Cloudflare R2 – VAŽNO pre produkcije

- [ ] Napravljen R2 bucket sa javnim pristupom
- [ ] Napravljen API token
- [ ] Env varijable dodate na Render

**Bez ovoga** slike koje prodavci ubace čuvaju se na disku Render servera, a taj disk se briše
pri svakom deploy-u i restartu – slike nestaju, a u bazi ostaju linkovi koji ne rade.
Lokalno (bez R2 varijabli) slike se i dalje čuvaju u `wwwroot/Resources/Images`.

### Uputstvo
1. <https://dash.cloudflare.com> → **R2 Object Storage** (besplatno do 10 GB, preuzimanje uvek besplatno;
   traži se kartica, ali se ne naplaćuje u okviru besplatnog limita).
2. **Create bucket**, npr. `pijacaudzepu-images`, lokacija Automatic (ili Eastern Europe).
3. U bucket-u **Settings → Public access**:
   - brzo rešenje: uključi **R2.dev subdomain** – dobijaš adresu tipa `https://pub-xxxx.r2.dev`
     (Cloudflare je ograničava, nije za veliki saobraćaj);
   - za produkciju: **Custom Domains → Connect domain**, npr. `slike.pijacaudzepu.rs`
     (domen mora biti na Cloudflare-u).
4. **R2 → Manage R2 API Tokens → Create API token**, dozvola **Object Read & Write**, samo za taj bucket.
   Zapiši **Access Key ID** i **Secret Access Key** (secret se vidi samo jednom).
   **Account ID** piše na glavnoj R2 stranici.
5. Na Render-u dodaj:

   | Env varijabla | Vrednost |
   |---|---|
   | `R2__AccountId` | Account ID |
   | `R2__AccessKeyId` | Access Key ID |
   | `R2__SecretAccessKey` | Secret Access Key |
   | `R2__Bucket` | `pijacaudzepu-images` |
   | `R2__PublicUrl` | javna adresa iz koraka 3, bez `/` na kraju |

   R2 se uključuje tek kad je svih pet podešeno; ako neka fali, ostaje lokalni disk.
6. Proba: kao prodavac promeni sliku proizvoda – u bucket-u treba da se pojavi fajl u `products/`,
   a slika treba da ostane i posle sledećeg deploy-a.

> Slike ubačene na Render pre ovoga su već izgubljene (ili će biti pri sledećem deploy-u) –
> prodavci treba da ih ponovo ubace. Seed slike (Unsplash) nisu pogođene.

---

## 8. Praćenje grešaka (Sentry) – preporučeno

- [ ] Napravljen Sentry projekat
- [ ] `SENTRY_DSN` dodat na Render

Bez ovoga greške iz produkcije vidiš samo u Render logovima (i to kratko). Sa Sentry-jem stižu na e-mail,
sa detaljima i brojem korisnika koje su pogodile.

1. <https://sentry.io> → besplatan nalog (Developer plan) → **Create Project** → platforma **ASP.NET Core**.
2. Kopiraj **DSN** (izgleda kao `https://…@o….ingest.sentry.io/…`).
3. Na Render-u dodaj `SENTRY_DSN` = taj DSN i pokreni deploy.
4. U Sentry-ju podesi **Alerts** (npr. e-mail za svaku novu grešku).

---

## 9. Backup baze (Neon) – proveri jednom

- [ ] Proveren period čuvanja istorije (point-in-time restore) na Neon planu
- [ ] Probno vraćanje urađeno

1. Neon konzola → projekat → **Settings → Storage / History retention**: vidi koliko dana unazad može da se vrati baza
   (besplatan plan ima kratak period – za prave porudžbine preporučen je plaćeni plan sa dužim).
2. Probaj vraćanje bez rizika: **Branches → Create branch** od stanja „pre sat vremena” i pogledaj podatke u njoj.
   Glavna baza se ne dira; posle obriši probnu granu.
3. Zapiši gde je uputstvo, da u hitnom slučaju ne tražiš.
