# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

"Pijaca u Džepu" is a farmers'-market ordering app. Customers browse products from vendors at markets in Novi Sad, and vendors manage products and orders. The domain language and most UI strings are in Serbian (Latin script), and identifiers often are too (`Voce`, `Povrce`, `kupac`, `tezga`).

## Repository layout

- `PijacaUDzepu.API/PijacaUDzepu.API/` holds the ASP.NET Core 8 Web API. It uses EF Core with PostgreSQL (Npgsql), ASP.NET Identity with JWT, and SignalR.
- `PijacaUDzepu.Client/` holds the Ionic 8 + Angular 20 client (NgModule-based, `standalone: false`). It is wrapped with Capacitor 8 for Android.
- `Dockerfile` builds the client, publishes the API, and copies the client's `www/` into the API's `wwwroot/`. In production the API serves the SPA, with a fallback to `index.html`, so the client calls the relative `/api`.

## Commands

### API (run from `PijacaUDzepu.API/PijacaUDzepu.API`)
```
dotnet run                                  # http://localhost:5000, Swagger at /swagger in Development
dotnet build
dotnet ef migrations add <Name>             # dotnet-ef 9.x is installed globally
dotnet ef migrations remove
```
Migrations are applied automatically on startup: `Seed.SeedData` calls `Database.MigrateAsync()` and then seeds the data.

### API tests (run from `PijacaUDzepu.API`)
```
dotnet test PijacaUDzepu.API.Tests          # integration tests against a real local Postgres
```
- `ApiFactory` (a `WebApplicationFactory<Program>`) drops and recreates the `PijacaUDzepu_Test` database on every run, then starts the whole API in Development, so migrations and the demo data run too. Use the `TEST_DATABASE_URL` env var to point it at another server.
- Each request gets its own `X-Forwarded-For`, so the login rate limit doesn't interfere.
- All test classes share the `api` collection, so they run sequentially on one database. Pick different vendors or products per test so tests don't affect each other.
- The covered flows are: the checkout split and fees, the courier accept race, stock overselling and its restore, a paused vendor, the mandatory reject reason, report totals, password rules, registration requiring phone and address, product image upload without a file extension, active/finished order tabs, the SuperAdmin closing an order left open, the courier flow (offered only after the vendor confirms, picked up, delivered or not delivered with a reason, and the customer's open-order count), lockout, forgot password, account deletion, the health endpoint and message translation (every `L.T` key has a translation; errors follow `Accept-Language`).
- The Dockerfile publishes only the API project, so the test project never reaches the image.

### Client (run from `PijacaUDzepu.Client`)
```
npm start                                   # ng serve; talks to http://localhost:5000/api
npm run build                               # production build -> www/
npm run lint                                # angular-eslint (eslint.config.js)
npm test                                    # Karma/Jasmine
npx ng test --include src/app/path/to/file.spec.ts   # single spec
npx tsc --noEmit -p tsconfig.app.json       # quick type-check
npm run build:android                       # ng build --configuration mobile + cap sync android
npm run open:android
```
Use `npm ci --legacy-peer-deps` for installs, the same as the Dockerfile does.

The environments differ only in `apiUrl`:
- `environment.ts` uses localhost:5000.
- `environment.prod.ts` uses the relative `/api`.
- `environment.mobile.ts` uses the absolute URL of the deployed Render API.

## Configuration

Documentation rule: every change also goes into this file, `README.md` and the user manual (`docs/uputstvo/`, a generated PowerPoint for customers, vendors and couriers - see its README).

`PODESAVANJA.md` (in Serbian) is the owner's checklist of features that need activation: Brevo email, Firebase push, Render deploy, courier fee, legal placeholders, and cleartext HTTP for the APK. Update it when adding anything that needs env vars or manual setup.


- The DB connection comes from the `DATABASE_URL` env var, falling back to `ConnectionStrings:DefaultConnection` in appsettings.
- The JWT key comes from `TOKEN_KEY`, falling back to `TokenKey`. Tokens are signed with HmacSha256.
- `AllowedOrigins` is a comma-separated list. The Capacitor origins `https://localhost` and `capacitor://localhost` are always added.
- The Docker image overwrites `appsettings.json` with logging config only, so production relies entirely on env vars.
- The database needs the Postgres `unaccent` extension, which a migration adds. Search uses `EF.Functions.ILike(EF.Functions.Unaccent(...))`.

### Local and Render at the same time
- **API:** `FrontendUrls` (`FrontendUrlResolver.DefaultFrontendUrls`) lists localhost:4200, localhost:8100 and Render. The list drives CORS together with the Capacitor origins.
- **Email links:** they use the request's `Origin` if it is on that list, otherwise `FrontendUrl`, which defaults to Render. The APK's origin `https://localhost` therefore always gets Render links.
- **Mobile app:** `environment.mobile.ts` reads the API server from `localStorage.apiServer` (`services/api-server.ts`) before bootstrap. The default is Render.
  - On the login page the mobile build shows "Server: … Promeni", where you can point the same APK at a LAN API, e.g. `http://192.168.x.x:5000`.
  - Switching servers clears the token, user and cart, then reloads.
- **LAN access:** the API's `https` launch profile listens on `http://0.0.0.0:5000`, so phones on the same Wi-Fi can reach it.

## Backend architecture

- **Controllers** inherit `Controllers/Base/BaseApiController`, which carries `[Authorize]` and the route `api/[controller]`. Public endpoints opt out with `[AllowAnonymous]`.
- **Authorization policies** are defined in `Extensions/ServiceCollectionExtensions.cs`:
  - `RequireSuperAdmin`
  - `RequireVendorAdmin` (VendorAdmin or SuperAdmin)
  - `RequireCustomer` (Customer or SuperAdmin)
  - `RequireCourier` (Courier or SuperAdmin)
- **Vendor identity:** a vendor user is linked to a `Vendor` through the `VendorUsers` join table. Vendor endpoints resolve the current vendor with `IVendorService.GetVendorIdForUser(User.GetUserId())`, and services scope every query by that `vendorId`.
- **Services** (`Services/` with interfaces in `Services/Interfaces/`) contain all business logic and are registered as scoped in `AddApplicationServices`. Register any new service there.
- **Error handling:**
  - Services throw `KeyNotFoundException`, `InvalidOperationException`, or `UnauthorizedAccessException` with Serbian messages wrapped in `L.T(...)`.
  - **Message language:** `Helpers/L.cs` translates every user-facing API message (errors, Identity errors, validation, the 429 and 500 bodies, and confirmations such as "Lozinka promenjena.") into the app's language. The client's `TokenInterceptor` sends `Accept-Language: sr|en|ru` from `TranslateService.lang`; a middleware at the top of `Program.cs` stores it in `L.Lang` (an AsyncLocal, default `sr`). The dictionary is keyed by the Serbian text; dynamic parts use `{0}` placeholders (`L.T("{0}: nema više na stanju.", name)`). Any new message needs `L.T` and an `en`/`ru` entry; `LocalizationTests` fails on a missing one. Emails and push notifications stay in Serbian.
  - The interceptor also fills in a translated `message` when the server sent none (no connection, 429, 5xx: keys `error.network`, `error.tooMany`, `error.generic`).
  - Controllers catch these and return `NotFound`/`BadRequest(new { message })`. The client reads `err.error?.message`.
  - `Middleware/ExceptionMiddleware` handles anything uncaught.
- **DTOs** live in `Models/DTO/Input` (with validation attributes) and `Models/DTO/Output`. Services map entities to DTOs with a private static `MapToDto`.
- **Enums** are serialized as strings (`JsonStringEnumConverter`) but stored as integers. The client mirrors them as TypeScript string-literal unions in `src/app/models/*.model.ts`. When you add or rename an enum value, update both sides.
- **Product categories** (`ProductCategory`): Voce, Povrce, ZacinskoBilje, Mahunarke, Pecurke, MlecniProizvodi, Jaja, MesniProizvodi, PcelinjiProizvodi, Zimnica, SuvoVoceIOrasasti, Konditori, KucnaHemija, Ostalo. New values go at the end of the enum (stored as integers). The display order and the single list for the home filter, the vendor form and the reports is `PRODUCT_CATEGORIES` in `product.model.ts`; labels are the `cat.*` translation keys (sr/en/ru).
  - Gotcha: migration `AddProductCategory` added the column with default 0 = Voce, so products created before it became "fruit". `Seed.FixDemoData` repairs demo products on every start (see Seed data).
- **Orders:**
  - A cart checkout is split into one `Order` per vendor, for both logged-in and guest orders (`CreateGuestOrders`).
  - `OrderItem` snapshots the product name, unit, and price.
  - `OrderHub` (`/hubs/orders`) puts each connection into the `user_{id}` group and, for vendors, also into the `vendor_{id}` group.
  - `OrderService` pushes `NewOrder` events to vendors and `OrderStatusChanged` events to customers.
  - SignalR receives the JWT through the `access_token` query string.
- **Checkout modes (`Order.Fulfillment`):**
  - `Pickup` is a reservation: the customer collects the goods at the stall. Couriers never see these orders, the address is optional, and the vendor completes the order.
  - `Courier` means the Eco courier delivers. The address is required, and `DeliveryFee` is charged per order, i.e. per vendor. The fee comes from `Delivery:CourierFee`, which can be overridden with the `Delivery__CourierFee` env var, and the client reads it from `GET /api/orders/delivery-options`.
  - `TotalAmount` covers only the goods; `AmountToPay` = goods + fee.
  - Vendors can attach a `Message` to a status change, stored as `VendorMessage`. It is pushed to the customer only with the change it was written for.
  - Rejecting requires a reason (the vendor's dialog won't close without one, and the API returns 400). The reason is also emailed in the background (`EmailService.SendOrderRejected`) to `GuestEmail` or the customer's account email, linking to `/prati/{code}` for guests and `/order-detail/{id}` for customers. The customer's pages (`my-orders`, `order-detail`, `prati`) show a rejected or not-delivered order with its items and amount struck through and the reason in a red box; in `my-orders` the whole card is red (`ion-card.failed`: red left border and tinted background) so it stands out in the list, and the live notification is a red toast (`rejectionText` in `NotificationService`).
  - SignalR uses `JsonStringEnumConverter` like the REST API, so enums arrive as strings on both channels.
- **Vendor opening hours** (`Helpers/VendorAvailability`, all in local Europe/Belgrade time via `Helpers/LocalTime`):
  - Fields: `Vendor.OrdersPaused`, `OpeningHours` (jsonb list of `{ day: 1=Mon … 7=Sun, from, to }`) and `ClosedDates` (jsonb list of dates). No opening hours means the vendor is always open.
  - The vendor edits them in "Moj profil → Radno vreme" (`PUT /api/vendors/my-vendor/availability`). Past closed dates are dropped and there is one entry per day.
  - `VendorDto` and `ProductDto` carry the current state: `IsOpen`/`VendorOpen`, `ClosedReason` (`Paused`/`ClosedDay`/`Hours`/`Inactive`) and `OpensAt` (local time). The home page shows "Zatvoreno · otvara se …" (`services/vendor-status.ts`) and disables adding to the cart.
  - `OrderService.BuildOrders` rejects an order containing items from a closed vendor, with a message that says when the vendor opens.
  - `AcceptsReservations`, `OffersDelivery`, `MinOrderAmount`, `DeliveryRadiusKm` and `DeliverySchedule` on `Vendor` are leftovers from an earlier plan in which vendors delivered themselves. Nothing reads them. The minimum order amount is deliberately not enforced (owner's decision). The delivery radius would need geocoding.
- **Customer extras** (`FavoriteService`, `FavoritesController`, `OrderService.RateOrder`, client `FavoritesService` and `OrderActionsService`):
  - **Favorites** (`Favorites` table): the client offers favorite products only, as a heart on the product picture. The server also supports favorite vendors (`vendorIds`), but the UI dropped them because two hearts on one card confused users. Guests keep favorites in `localStorage.favorites`; on login they are merged into the account (`POST /api/favorites/sync`). The home ♥ filter passes `ids` to `GET /api/products`. With those parameters, unavailable favorite products are returned too; without them, only available products.
  - **"Obavesti me"** (`StockAlerts`, logged-in users only). When `ProductService.UpdateProduct` makes a product available again, `NotifyBackInStock` sends `StockAvailable` (SignalR) and a push, then deletes the alerts.
  - **Ratings** (`OrderRatings`, one per order, 1–5 for the vendor plus the courier on courier orders, optional comment) are allowed only for `Completed` orders, via `POST /api/orders/{id}/rating` or `/api/orders/track/{code}/{id}/rating` for guests. `OrderDto.Rating`; the vendor gets `OrderRated`. `ProductDto.VendorRating`/`VendorRatingCount` are filled for a whole page in one grouped query (`FillVendorRatings`). The client modal is `shared/rating-form`, and `StarsPipe` renders ★★★★☆.
  - **"Poruči ponovo"** is client-only (`OrderActionsService.reorder`): it reloads each item's product and adds only those that are available and whose vendor is open.
- **Vendor tools:**
  - **Stock** (`Product.StockQuantity`, null = not tracked): `OrderService.ReserveStock` runs in the same transaction as the order insert, with one conditional `ExecuteUpdate` per product (`StockQuantity >= qty`), so concurrent orders can't oversell. Reaching 0 sets `IsAvailable = false`.
    - `RestoreStock` gives the quantity back on Rejected or Cancelled. It re-enables only products that were at 0 and unavailable (a product the vendor turned off by hand stays off) and fires the back-in-stock alerts.
    - The home card shows "Na stanju: X" and the + button stops at the stock.
  - **Quick edit:** `PATCH /api/products/{id}/quick { price?, stockQuantity?, clearStock?, isAvailable? }` is used by the vendor's product list (price/stock dialog and the availability toggle). The full `PUT` sends every field and would reset the stock.
    - Gotcha (duplicate products): `vendor-product-form` used to reset `saving` on success and only then navigate, so a second tap created the product twice, and Ionic kept the old form in its stack, so "Dodaj proizvod" could reopen it with the previous product's data. Now `save()` returns while `saving`, stays locked until it leaves via `NavController.navigateBack`, and `ionViewWillEnter` resets the whole form. Keep this pattern for any form that creates records.
    - Gotcha: `vendor-products` rows are `ion-item`s with `routerLink`, which Ionic renders as links, so buttons inside a row need both `stopPropagation()` and `preventDefault()`.
  - **Pregled dana** (`/vendor-today`, `GET /api/orders/vendor-today`): items to prepare summed across active non-delivery orders, products with stock ≤ 3, and today's counts and revenue in local time.
- **Guest tracking:**
  - A guest checkout gets a random `TrackingCode`, shared by all orders of that checkout. It is returned only in the guest-create response, never in vendor or courier views.
  - `GET /api/orders/track/{code}` returns the orders with the address and phone partly masked. `PUT .../track/{code}/{id}/cancel` cancels a pending order.
  - Customer and guest cancellation is temporarily disabled: `OrderService.CustomerCancelEnabled` and the client's `CUSTOMER_CANCEL_ENABLED` (`order.model.ts`) must be switched together.
  - The link works while any order is active and for 30 days after the last change.
  - Live updates go through the anonymous `TrackingHub` (`/hubs/tracking`, `Subscribe(code)`).
  - If the guest enters an optional email, `EmailService.SendTrackingLink` sends the link from a background task, so checkout neither waits for SMTP nor fails because of it. It is skipped with a log warning when `Email:SmtpUser`/`SmtpPass` are empty. Links use `FrontendUrl`.
  - The client page is `/prati/:code`. The device remembers its codes in `localStorage.guestOrders` (`GuestOrdersService`), so `/prati` lists the guest's orders.
- **Support requests:**
  - The public form `/podrska` posts to `POST /api/support`. It is anonymous, but a token links the request to the user, and the endpoint is limited to 5 requests per 10 minutes per IP (`X-Forwarded-For` aware). A filled hidden `website` field is silently dropped.
  - Requests are stored in `SupportRequests`. SuperAdmins (in the `admins` SignalR group) get `NewSupportRequest` and a push notification.
  - A copy is emailed in the background to `Support:Email` (default zivkovic_kac@live.com), with Reply-To set to the user's address.
  - SuperAdmins manage requests at `/admin-support`.
- **Couriers:**
  - A SuperAdmin creates courier accounts (`POST /api/auth/create-courier`). The courier then fills in their own details in `CourierProfile`, which holds the vehicle and license plate; name and phone live on `User`.
  - **Flow:** customer orders → vendor confirms → only then do on-shift couriers get `CourierNewOrder` and the alarm (`NotifyCouriersNewOrder`, called from `UpdateOrderStatus` on `Confirmed`) → a courier accepts (the order is his) → "Preuzeo sam robu" (`InDelivery`, the customer sees "Kurir je preuzeo robu") → "Isporučeno" (`Completed`) or "Nije dostavljeno". Couriers see and accept only `CourierOfferStatuses` (Confirmed, ReadyForPickup), never `Pending`. The customer gets "Dodeljen je kurir" on accept, not "preuzeo". When the vendor marks an unassigned courier order "Spremno", `OrderStatusChanged` also goes to the `couriers` group, so the "Nove" tab shows "Spremno za preuzimanje" without a reload.
  - What each side sees: the vendor's pending courier order says "Kuriri je dobijaju kada potvrdite"; the customer sees "Čeka se kurir" only while the order is Confirmed/ReadyForPickup without a courier (not while Pending, not after it is closed). Couriers can no longer hold `Pending` orders; ones accepted under the old rule still show "Čeka potvrdu prodavca". A status change that lost a race (e.g. the vendor presses "Spremno" after the courier picked up) returns 400 with Serbian/translated status names (`OrderService.StatusName`), and the vendor page reloads.
  - `OrderHub` also adds every Courier connection to the `couriers` group.
  - Accepting is a single conditional `ExecuteUpdateAsync` (`CourierId IS NULL` and the order still active), so only the first courier wins and the rest get 409. Accepting, rejecting, or cancelling sends `CourierOrderUnavailable`, and the vendor and customer get `CourierAssigned`.
  - Before accepting, couriers see the pickup point and delivery address but not the customer's name or phone (`MapToCourierPreview`).
  - Every order stores `ContactPhone` and `DeliveryAddress`, for guests and logged-in customers alike.
  - **Multi-stall checkouts:** every order of one cart shares `Order.CheckoutId`, set in `BuildOrders`. The courier's "Nove" tab groups available orders by it, and "Prihvati sve" calls `POST /api/couriers/checkouts/{id}/accept`, which accepts each order separately, so one taken by another courier doesn't block the rest. "Moje" shows a route (stalls, then the customer) with a Google Maps directions link built from `OrderDto.MarketAddress` waypoints.
  - **Daily settlement:** `GET /api/couriers/settlement?date=` (the "Obračun" tab) covers orders delivered that local day: collected (goods + fee), owed to vendors (goods, per vendor) and earned (fees).
  - Gotcha: the courier page's `availableGroups`/`routes` getters are memoized. Returning new arrays on every change-detection pass made `*ngFor` recreate the Ionic cards in a loop and froze the page.
  - Only couriers with `CourierProfile.IsOnShift` get new-order notifications and can accept orders.
  - Delivery flow: the courier sets Confirmed/ReadyForPickup → `InDelivery` (picked-up), then `InDelivery` → `Completed` (delivered). A vendor cannot complete an order that has a courier.
  - **Not delivered:** from `InDelivery` the courier can press "Nije dostavljeno" (`POST /api/couriers/orders/{id}/not-delivered { reason }`, reason required in the dialog and the API). Status `NotDelivered` (added at the end of the enum), `Order.NotDeliveredReason`/`NotDeliveredAt`; stock is restored (the goods go back to the stall), the customer gets a push and a red box with the reason, the vendor and SuperAdmins get a push, and the vendor card shows the reason. It counts as finished, not delivered (not in the settlement).
  - **Customer progress:** `shared/order-progress` (`<app-order-progress>`) on `order-detail` and `/prati` shows the steps with times (`createdAt`, `RespondedAt` — now in `OrderDto`, `CourierAssignedAt`, `PickedUpAt`, `DeliveredAt`); keys `progress.*`. A step is ticked only if it really happened (timestamps); an order closed by a SuperAdmin ends with "Zatvorio administrator" instead of "Dostavljeno". The waiting-order alerts and the `/waiting` report measure the wait for a courier from `RespondedAt` (vendor confirmation), not from `CreatedAt`.
  - `OrderStatus.InDelivery` and then `NotDelivered` were added at the end of the enum, because statuses are stored as integers. Every client status map (`getStatusLabel`/`getStatusColor` on the vendor, courier, customer and admin pages, `status.*` keys) must know them.
- **Order alarms (ring until handled):**
  - The vendor's phone rings while the vendor has `Pending` orders; an on-shift courier's phone rings while there are available (vendor-confirmed) orders the courier hasn't declined. Couriers can decline (`POST /api/couriers/orders/{id}/decline`, stored in `CourierDeclines`), which hides the order only for them.
  - In the app, `NotificationService.updateAlarm()` loops `SoundService.play('alarm')` based on `pendingCount$` / `courierAvailable$`, only while the page is visible. `PendingChanged` (SignalR, vendor group) stops it on the vendor's other devices.
  - In the background, the server sends data-only FCM messages (`PushService.SendAlarm` / `CancelAlarm`, tags `order_{id}` and `courier_order_{id}`). The native `AlarmMessagingService` (Android) shows them as `FLAG_INSISTENT` notifications on the `order_alarms` channel, cancels them on `alarm-cancel`, and forwards every message to the Capacitor push plugin, whose own service is removed in the manifest. `MainActivity` clears the alarms when the app comes to the foreground.
- **Reports (SuperAdmin, `/admin-reports`):** `ReportsController` (`GET /api/reports/orders|customers|couriers?from=yyyy-MM-dd&to=yyyy-MM-dd`; without `from` the report starts at the first order, without `to` it ends today; the page opens with both dates empty = everything) → `ReportService`. Periods are local Europe/Belgrade dates, inclusive, filtered on `Order.CreatedAt`. "Promet" counts only `Completed` orders. Guests are grouped by the last 9 digits of `ContactPhone`. The service loads only the needed columns and groups in memory.
- **More reports:** `GET /api/reports/vendors` (orders, rejection rate, last 3 rejection reasons, average rating, and average response time from `Order.RespondedAt`, which is set when an order first leaves `Pending`), `/products` (completed items per product, vendor and unit, plus revenue per category) and `/waiting` (orders older than 15 minutes still waiting for the vendor or a courier). Every report table has an "Izvoz (Excel)" button (`shared/csv.ts`: UTF-8 BOM, `;` separator, decimal comma, so Serbian Excel opens it correctly).
- **Active / finished tabs:** the vendor's orders page has the tabs "Aktivne" and "Završene" (`GET /api/orders/vendor-orders?finished=`), the courier page "Nove | Moje | Završene | Obračun" (`GET /api/couriers/my-orders?finished=`). Both use `OrderService.OpenStatuses` (Pending, Confirmed, ReadyForPickup, InDelivery); the older `ActiveStatuses` leaves out `InDelivery` on purpose (courier availability, alarms), so don't use it for these lists. The courier tab labels are shrunk in `courier-orders.page.scss` so four fit on a 360 px phone.
- **Closing an order left open (SuperAdmin):** `/admin-orders` ("Otvorene porudžbine", `GET /api/orders/open`, oldest first, paged) with a "Završi" button (`POST /api/orders/{id}/admin-complete`). It sets `Completed` and the trail `Order.ClosedByAdminId`/`ClosedByAdminAt` (FK SetNull), stops the vendor and courier alarms and notifies vendor and courier but sends the customer no push. `OrderDto.ClosedByAdminName`/`ClosedByAdminAt` show "Zatvorio administrator {name} · date" on vendor and courier cards; customer pages show only `order.closedByAdmin` (the guest tracking DTO drops the name).
- **Waiting-order alerts:** the `OrderWatchdog` hosted service runs every minute. It finds orders that have been `Pending` longer than `Alerts:PendingMinutes` (default 15), and courier orders still without a courier longer than `Alerts:CourierMinutes` (default 20) after the vendor confirmed them (`RespondedAt`). For each one it sends `OrderWaiting` to the `admins` SignalR group plus a push to the SuperAdmins, and re-rings the vendor (`SendAlarm`). Each order is alerted only once (`Order.WaitAlertSentAt`). The migration that added the column marks all existing orders as already alerted.
- **Push notifications:** `PushService` sends through Firebase Cloud Messaging to `DeviceTokens`, which the mobile app registers via `POST /api/devices`. Push is enabled only when `FIREBASE_CREDENTIALS_JSON` or `FIREBASE_CREDENTIALS_PATH` is set; otherwise it is silently skipped. The Android app also needs `android/app/google-services.json` (Firebase project `pijacaudzepu`); the repo is public, so the file is in `.gitignore` and lives only locally. `environment.mobile.ts` has `pushEnabled: true`, so building the APK without that file crashes the app on login, and with push on, a backgrounded app leaves ringing to the server's FCM alarms (`FIREBASE_CREDENTIALS_JSON` must be set on Render). The client's `PushService` creates the `orders` channel, which must match the server's `AndroidChannelId`.
- **Sounds:** `SoundService` generates notification sounds with Web Audio. It unlocks the `AudioContext` on the user's first tap, because browsers block audio before any user interaction.
- **Vendor onboarding:** a SuperAdmin creates vendors and sends email invitations (`EmailService`, SMTP via Brevo). The invited user completes registration through `AcceptInvitation`, which the client handles on the `setup-account` page.
- **Images** are resized to JPEG with `Helpers/ImageResizer` and saved through `IImageStorage`:
  - `R2ImageStorage` (Cloudflare R2 via AWSSDK.S3) is used when all `R2__*` env vars are set; the DB stores the absolute public URL. Render's disk is wiped on deploy, so production needs R2.
  - Otherwise `LocalImageStorage` writes to `wwwroot/Resources/Images` and the DB stores the relative `/Resources/...` path.
  - Order matters for consistency: save the new image, save the DB, then delete the old one (delete the new one if the DB save fails). `Delete` ignores URLs the storage didn't create, such as the seed data's Unsplash URLs.
  - The client's `getImageUrl` handles both relative and absolute URLs.
  - **Uploads from phones:** the product form shrinks the picked photo in the browser (`shared/image-file.ts` `shrinkImage`: max 1600 px, JPEG) and previews it before saving, because phone photos are often over the 5 MB limit or (Android gallery) have no file extension. If the upload fails, the form stays open with the server's message instead of reporting success. The upload endpoints accept a name without an extension when the content type is `image/*`; `ImageResizer` rejects anything that isn't a real image ("Neispravan format slike.").

## Seed data

`DataAccess/SeedData/SeedData.cs` (`Seed.SeedData(services, isDevelopment)`) always migrates the database and creates the roles. Everything else depends on the environment:
- **Development, or `DEMO_ACCOUNTS=true`:** creates the demo accounts if missing, and the four markets with stalls, vendors and products when no market exists yet:
  - `admin` / `admin1234` (SuperAdmin)
  - `kupac` / `kupac1234` (Customer)
  - `kurir1`, `kurir2` / `kurir1234` (Courier)
  - nine vendor users (`marko`, `jovana`, `nikola`, `milan`, `jelena`, `dragan`, `ana`, `bojan`, `ivana`) / `vendor1234` (VendorAdmin)
- **Production without `DEMO_ACCOUNTS`:** no demo data. Demo accounts that still have their default password are locked (`LockoutEnd = DateTimeOffset.MaxValue` marks a seed lock; login says the account is disabled). Running in demo mode unlocks them again. The value is trimmed and case-insensitive (`true`/`1`/`yes`/`da`), and every start logs `Test nalozi: uključeni|zaključani (DEMO_ACCOUNTS=…)`, so Render's log shows whether the variable arrived.
- **Demo data fix-up (`FixDemoData`, every start, every environment):** demo products and markets get the corrected image and category, but only rows that still hold the old seed value (`DemoProductImageFixes`, `DemoMarketImageFixes`, `DemoCategoryFixes`), so a vendor's own changes stay. Product images come from Wikimedia Commons (absolute `upload.wikimedia.org` 500px thumbnails) or Unsplash; authors and licenses are listed in `docs/slike-proizvoda.md`. When you change a seed image, check that it really shows the product and add the old→new pair to the fix-up table.
- **Admin in production:** created from `ADMIN_PASSWORD` if it doesn't exist. If `admin` still has `admin1234` and `ADMIN_PASSWORD` is set, the password is reset to it; without the variable the server logs a critical warning on every start.

## Error tracking

Sentry (`Sentry.AspNetCore`) is enabled only when `SENTRY_DSN` is set (`Program.cs`, no PII, no tracing). `ExceptionMiddleware` handles exceptions before Sentry's middleware sees them, so it calls `SentrySdk.CaptureException` itself; without a DSN that call does nothing.

## Security

- Passwords need at least 8 characters and a lowercase letter (Identity options + `[MinLength(8)]` on the input DTOs + the client forms). Identity error messages are Serbian (`Helpers/SerbianIdentityErrorDescriber`).
- Login uses lockout: 10 wrong passwords lock the account for 15 minutes (`CheckPasswordSignInAsync(..., lockoutOnFailure: true)`).
- Rate limits (`Program.cs`, partitioned by the first `X-Forwarded-For` address): `auth` = 10 requests/minute on login and register, `support` = 5 per 10 minutes on the support form.
- `GET /api/auth/health` is anonymous and returns only `{ status, timestamp }`.
- **Forgot password:** `POST /api/auth/forgot-password { login }` (username or email) always returns the same message, so it can't be used to find accounts. If the account has an email, a `PasswordReset` row is created (only the SHA-256 hash of the token is stored; valid 2 hours, single use) and `EmailService.SendPasswordReset` emails `/reset-password?token=…` in the background. In Development, without SMTP, the link is logged instead. `POST /api/auth/reset-password { token, newPassword }` sets the password, clears a failed-login lockout and invalidates the user's other links. Identity's own reset tokens are not used, because Render loses the data-protection keys on every deploy.
- **Admin reset:** `POST /api/users/{id}/reset-password` (SuperAdmin) sets a random 10-character temporary password, unlocks the account and returns the password once. This covers couriers and vendors without email.
- **Customer address:** registration (`RegisterDto`) requires `PhoneNumber` and `Address`; `Floor` and `Apartment` are optional. They are stored on `User` and returned in `UserDto`. `PUT /api/auth/profile { phoneNumber, address?, floor?, apartment? }` changes them from "Moj nalog" (the address fields are shown to customers only). The cart fills phone and delivery address from the user, joined into one line by `deliveryAddressOf` (`user.model.ts`, "Dunavska 5, sprat 2, stan 7", in Serbian because vendors and couriers read it); the customer can still change them for one order. The cart and the account page refresh the user via `GET /api/auth/me`, because a user saved in `localStorage` before this change has no address. The demo `kupac` is seeded with a phone and address.
- **Account deletion** (Google Play requirement): `POST /api/auth/delete-account { password }`. It is blocked while the user has active orders as a customer or courier, and for the last SuperAdmin. The customer's orders are kept but anonymized (`CustomerId` null, `GuestName` "Obrisan nalog", no email, phone, address or note); the rest is removed by cascade or set to null. The client page is `/account` ("Moj nalog", all roles), which also has the phone/address and the password change.

## Client architecture

- Each page is a lazy-loaded Ionic module, registered in `app-routing.module.ts`.
  - **Vendors and couriers don't shop:** `POST /api/orders` and `/api/orders/guest` return 403 ("Prodavci i kuriri ne mogu da poručuju robu…") for `VendorAdmin`/`Courier` (SuperAdmin may still order). On the client, `ShopGuard` on `home`, `cart`, `my-orders`, `order-detail`, `prati` sends them to `AuthService.staffHome` (`/vendor-products` or `/courier-orders`, the same pages as after login), and the menu hides "Početna" and the cart (`AuthService.isStaff`). `<app-cart-button>` also hides itself for them, because the shared pages (`/podrska`, `/legal/*`) carry it in their header.
  - Pages that need a logged-in user use `AuthGuard`.
  - Role-restricted pages use `RoleGuard` with `data: { roles: [...] }`.
- `interceptors/token.interceptor.ts` attaches the JWT, which `AuthService` keeps in `localStorage` (`token`, `user`).
- **i18n:** `services/translate.service.ts` holds an inline dictionary per language (`sr`, `en`, `ru`). Templates use the `t` pipe (`'key' | t`) from `pipes/translate.pipe.ts`, exported through `SharedModule`. New user-facing text on customer pages needs keys in all three languages. The vendor and admin pages still mostly use hardcoded Serbian strings.
- **Pagination:** lists that grow (vendor orders, admin users, support requests, a courier's finished deliveries) are paged on the server (`skip`/`take` → `PagedResult<T>` with `TotalCount`, `take` clamped to 100) and use the shared `<app-pager>` (`shared/pager`, in `SharedModule`): page sizes 10/20, the chosen size remembered per list via `storageKey`, hidden while everything fits on the smallest page. Report tables are already loaded in full and are paged on the client with `pageOf()`. The catalog and the customer's "Moje porudžbine" keep server-paged infinite scroll. `GET /api/users` takes `role` and `search` and loads roles for the whole page in one query.
- **Search bars:** `global.scss` overrides every `ion-searchbar`: a smaller gap for the magnifier (Ionic's md mode leaves 55px), a full-opacity placeholder and an ellipsis, because the placeholder was cut off or too pale on narrow phones and with a larger system font. Keep search placeholders short (`home.searchProduct` = "Traži proizvod…").
- **Orders button:** `shared/orders-button` (`<app-orders-button>`, top right in the vendor page headers and next to `<app-cart-button>` in the customer page headers, hidden ≥768 px where the top menu has the link with the same badge) opens the orders outside the hamburger menu. For a vendor (`VendorAdmin`) it shows the red count of `Pending` orders (`NotificationService.pendingCount$`, the same number that drives the alarm), pulses when it grows and links to `/vendor-orders`; the vendor modules import `SharedModule` for it. For customers it shows a red count of open orders (`GET /api/orders/my-active-count`, `OpenStatuses`), kept in `NotificationService.customerActive$`; it reloads on `OrderStatusChanged`/`CourierAssigned` and after a checkout in the cart, and `customerBump$` makes the badge pulse. Guests with saved tracking codes get the icon without a count, linking to `/prati`.
- `CartService` keeps the cart on the client, in `localStorage` (`cart`). `NotificationService` wraps the SignalR connection. It reconnects indefinitely (the default `withAutomaticReconnect()` gives up after ~42 s) and emits `resync$` after a reconnect or when the app becomes visible, so the vendor and courier order pages reload events they missed.
