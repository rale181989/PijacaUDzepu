# Pijaca u Džepu

Aplikacija za poručivanje sa novosadskih pijaca. Kupci biraju robu sa tezgi i preuzimaju je sami
ili je dobijaju na adresu preko Eko kurira. Prodavci vode ponudu i porudžbine, kuriri dostave, a
administrator nalog prodavaca, kurira i izveštaje.

Radi kao web aplikacija i kao Android aplikacija (Capacitor). Produkcija: <https://pijacaudzepu.onrender.com>.

## Funkcije

**Kupac** (nalog nije obavezan)
- Katalog sa filterima (pijaca, prodavac, grupa), pretragom bez kvačica (polje čitljivo i na uskim telefonima) i sortiranjem; kod sortiranja po ceni bira se jedinica mere
- Grupe proizvoda: voće, povrće, začinsko bilje, mahunarke, pečurke, mlečni proizvodi, jaja, meso, med i pčelinji proizvodi, zimnica, suvo voće i orašasti plodovi, kolači, kućna hemija, ostalo
- Korpa sa robom više prodavaca; svaki prodavac dobija svoj deo porudžbine
- Rezervacija (preuzimanje na tezgi) ili dostava Eko kurirom
- Registracija traži telefon i adresu (sprat i stan opciono); upisuju se sami u korpu, a menjaju se u „Moj nalog”
- Praćenje porudžbine bez naloga (link, opciono e-mail), statusi uživo
- Na početnoj kratko „Kako radi”; „Dodaj” odmah dodaje 1, a kartica pokazuje koliko je u korpi; pri dodavanju sa nove tezge aplikacija kaže da se dostava plaća po tezgi
- Pretraga razume jedninu i sitne greške („jabuka” nalazi „Jabuke”); filter nudi samo grupe i tezge u kojima ima robe
- Korpa: prodavac, pijaca i tezga uz svaku stavku, ukupno sa dostavom, šta još treba upisati; posle poručivanja „Hvala!” i ukupan iznos cele kupovine
- Detalji porudžbine: gde se preuzima (pijaca, tezga, adresa, „Kako da stignem”) i „Pozovi prodavca”
- Registracija sa opcionim e-mailom (i u „Moj nalog”), uz dobrodošlicu; gostujuće porudžbine sa tog telefona prelaze na nalog
- Moje porudžbine i van menija: ikonica gore desno pored korpe sa crvenim brojem porudžbina u toku (menja se uživo i „poskoči” kad stigne promena); odbijena ili nedostavljena porudžbina je cela crvena u spisku, precrtana, sa razlogom prodavca ili kurira
- Tok porudžbine korak po korak, sa vremenom: poručeno → prodavac potvrdio → dodeljen kurir → kurir preuzeo robu → dostavljeno (rezervacija: rezervisano → potvrđeno → spremno → preuzeto)
- Omiljeni proizvodi – srce na slici (i bez naloga), filter „Omiljeni” i „Obavesti me kad bude dostupno”
- Ocena prodavca i kurira posle preuzimanja; prosečna ocena prodavca na karticama proizvoda
- „Poruči ponovo” – stavke prethodne porudžbine u korpu
- Jezici: srpski, engleski, ruski – i poruke o greškama sa servera stižu na izabranom jeziku; iznosi u srpskom formatu („1.200 din”), ispravna množina („2 stavke”)
- Pristupačnost: dozvoljeno uvećavanje, vidljiv fokus tastature, natpisi na dugmadima za čitače ekrana
- Sajt može da se instalira kao aplikacija (i na iPhone-u: Safari → Podeli → „Dodaj na početni ekran”); link poslat na WhatsApp/Viber prikazuje logo i opis
- Ikonica aplikacije, ekran pri pokretanju i favicon su iz loga; verzija aplikacije piše u „Moj nalog”; dok se spisak učitava, prikazuju se sive kartice umesto spinera

**Prodavac**
- Proizvodi: cena, jedinica, grupa, klasa, slika, dostupnost; dupli dodir na „Sačuvaj” ne pravi duplikat (fotografija sa telefona se smanjuje već u aplikaciji i odmah se vidi na formi; ako slanje slike ne uspe, forma to jasno kaže)
- Porudžbine i van menija: ikonica gore desno na svim stranicama prodavca, sa crvenim brojem porudžbina na čekanju (raste odmah kad stigne nova i „poskoči”)
- Porudžbine u tabovima „Aktivne” i „Završene” (završene, odbijene, otkazane), po stranama; zvono dok ima porudžbina na čekanju
- Posle prijave prodavac stiže na porudžbine; telefon kupca se poziva dodirom
- Potvrdi / odbij (gotovi razlozi jednim dodirom ili svoj tekst; kupac razlog dobija i e-mailom) / spremno (jedan dodir) / kupac preuzeo (uz potvrdu)
- Radno vreme po danima, neradni dani i pauza („privremeno ne primam porudžbine”, važi odmah); „Samo danas”: „Danas ne radim” i „Danas zatvaram u …” bez menjanja nedeljnog rasporeda; van radnog vremena kupac vidi kad se tezga otvara i ne može da poruči
- Količina na stanju (opciono): porudžbine je umanjuju, na nuli proizvod izlazi iz ponude
- Brza izmena cene i stanja na listi proizvoda (dugme „Cena”, nazvana polja); proizvod van ponude je posivljen
- Pregled dana: šta treba spremiti (samo prihvaćeno, zbir po proizvodu), šta još čeka vaš odgovor, šta je pri kraju, današnji promet
- Prodavac ne vidi kupčev deo aplikacije (ponudu, korpu) i ne može da poručuje

**Eko kurir**
- Smena; zvono za nove dostave dok ih ne prihvati ili odbije
- Dostavu dobija tek kada je prodavac potvrdi; prihvati (prvi dobija dostavu, od tada je njegova) ili odbij (sklanja se samo njemu)
- Preuzeo sam robu (kupac vidi da je roba kod kurira) → isporučeno, ili „Nije dostavljeno” uz obavezan razlog (vide ga kupac, prodavac i administrator; stanje se vraća); tabovi „Moje” (dostave u toku) i „Završene” (po stranama)
- Porudžbina od više tezgi iste korpe: grupisano, „Prihvati sve”, ruta sa svim tezgama u Google mapama
- Dnevni obračun: naplaćeno od kupaca, koliko se predaje kom prodavcu, zarada
- Kurir ne vidi kupčev deo aplikacije i ne može da poručuje

**Administrator**
- Pijace, tezge, prodavci (pozivnice e-mailom), korisnici (pretraga, po stranama), kuriri
- Tehnička podrška (pitanja sa sajta)
- Izveštaji: porudžbine, kupci, kuriri (gde i koliko su vozili), prodavci (promet, odbijanja i razlozi, vreme odgovora, ocena) i najprodavaniji proizvodi – za izabrani period ili sve; svaka tabela se izvozi u Excel
- Upozorenje kada porudžbina predugo čeka prodavca (15 min) ili kurira (20 min); prodavcu ponovo zazvoni
- Otvorene porudžbine (najstarije prve): porudžbinu koja je slučajno ostala otvorena administrator završava jednim dugmetom; ostaje trag „Zatvorio administrator … · datum” koji vide prodavac, kurir i kupac

**Nalog (sve uloge)**
- „Zaboravili ste lozinku?” – link za novu lozinku na e-mail (važi 2 sata, jednom)
- Moj nalog: telefon i adresa za dostavu, promena lozinke i brisanje naloga (porudžbine ostaju prodavcima bez ličnih podataka)
- Administrator može postaviti privremenu lozinku korisniku bez e-maila

**Bezbednost**
- Lozinke najmanje 8 znakova; posle 10 pogrešnih pokušaja prijava se zaključava na 15 minuta, ali samo sa uređaja/adrese sa koje su stigli (napadač ne može da zaključa tuđi nalog)
- Ograničen broj pokušaja prijave po IP adresi
- U produkciji nema test naloga; admin lozinka iz `ADMIN_PASSWORD`

## Tehnologije
- **API:** ASP.NET Core 8, EF Core + PostgreSQL (Neon), ASP.NET Identity + JWT, SignalR, Firebase Cloud Messaging, MailKit (Brevo), Cloudflare R2 za slike
- **Klijent:** Ionic 8 + Angular 20, Capacitor 8 (Android)
- **Hosting:** Render (Docker); API servira i web klijent

## Pokretanje lokalno
Potreban je PostgreSQL na `localhost:5432` (podešavanje u `appsettings.json`), .NET 8 SDK i Node.js.

```
cd PijacaUDzepu.API/PijacaUDzepu.API
dotnet run                    # http://localhost:5000, Swagger na /swagger; migracije i test podaci se prave sami

cd PijacaUDzepu.Client
npm ci --legacy-peer-deps
npm start                     # http://localhost:4200 (ili: npx ng serve --port 8100)
```

Test nalozi (lokalno u Development režimu, a na Render-u samo uz `DEMO_ACCOUNTS=true` – bez nje javljaju „Nalog je onemogućen”): `admin`/`admin1234`, `kupac`/`kupac1234`,
`kurir1`/`kurir1234`, `marko`/`vendor1234` (i ostali prodavci sa istom lozinkom).

Android: `npm run build:android`, pa `npm run open:android` (Android Studio).

## Testovi
```
cd PijacaUDzepu.API
dotnet test PijacaUDzepu.API.Tests     # pokreće ceo API nad posebnom bazom PijacaUDzepu_Test (lokalni Postgres)
```
Pokrivaju: podelu korpe po prodavcima i iznose, „prvi kurir dobija porudžbinu”, stanje (bez prodaje preko stanja),
zatvorenu tezgu, obavezan razlog odbijanja, izveštaje, lozinke, zaključavanje, zaboravljenu lozinku, obavezne telefon i adresu pri registraciji, sliku proizvoda bez ekstenzije u imenu, razdvajanje aktivnih i završenih porudžbina, zabranu kupovine prodavcima i kuririma, nalaze iz testiranja agentima (`docs/testiranje/`), zatvaranje zaostale porudžbine od strane administratora, tok dostave (kurir tek posle potvrde prodavca), „nije dostavljeno” sa razlogom i brisanje naloga.

## Praćenje grešaka
Ako je na serveru podešen `SENTRY_DSN`, greške iz produkcije stižu u Sentry (sa detaljima, bez ličnih podataka).

## Dokumentacija
- [`CLAUDE.md`](CLAUDE.md) – arhitektura, pravila i komande za razvoj
- [`PODESAVANJA.md`](PODESAVANJA.md) – šta treba podesiti u produkciji (env varijable, Firebase, Brevo, R2…)
- [`docs/uputstvo/`](docs/uputstvo/) – korisničko uputstvo (PowerPoint) za kupce, prodavce i kurire, pravi se iz koda

Fotografije demo proizvoda: izvori, autori i licence su u [docs/slike-proizvoda.md](docs/slike-proizvoda.md).
