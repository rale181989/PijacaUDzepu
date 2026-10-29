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
Migrations are applied automatically on startup: `Seed.SeedData` calls `Database.MigrateAsync()` and then seeds the data. No backend test project exists.

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
  - Services throw `KeyNotFoundException`, `InvalidOperationException`, or `UnauthorizedAccessException` with Serbian messages.
  - Controllers catch these and return `NotFound`/`BadRequest(new { message })`. The client reads `err.error?.message`.
  - `Middleware/ExceptionMiddleware` handles anything uncaught.
- **DTOs** live in `Models/DTO/Input` (with validation attributes) and `Models/DTO/Output`. Services map entities to DTOs with a private static `MapToDto`.
- **Enums** are serialized as strings (`JsonStringEnumConverter`) but stored as integers. The client mirrors them as TypeScript string-literal unions in `src/app/models/*.model.ts`. When you add or rename an enum value, update both sides.
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
  - SignalR uses `JsonStringEnumConverter` like the REST API, so enums arrive as strings on both channels.
- **Guest tracking:**
  - A guest checkout gets a random `TrackingCode`, shared by all orders of that checkout. It is returned only in the guest-create response, never in vendor or courier views.
  - `GET /api/orders/track/{code}` returns the orders with the address and phone partly masked. `PUT .../track/{code}/{id}/cancel` cancels a pending order.
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
  - `OrderHub` also adds every Courier connection to the `couriers` group. Each new order sends `CourierNewOrder` to that group.
  - Accepting is a single conditional `ExecuteUpdateAsync` (`CourierId IS NULL` and the order still active), so only the first courier wins and the rest get 409. Accepting, rejecting, or cancelling sends `CourierOrderUnavailable`, and the vendor and customer get `CourierAssigned`.
  - Before accepting, couriers see the pickup point and delivery address but not the customer's name or phone (`MapToCourierPreview`).
  - Every order stores `ContactPhone` and `DeliveryAddress`, for guests and logged-in customers alike.
  - Only couriers with `CourierProfile.IsOnShift` get new-order notifications and can accept orders.
  - Delivery flow: the courier sets Confirmed/ReadyForPickup → `InDelivery` (picked-up), then `InDelivery` → `Completed` (delivered). A vendor cannot complete an order that has a courier.
  - `OrderStatus.InDelivery` was added at the end of the enum, because statuses are stored as integers.
- **Push notifications:** `PushService` sends through Firebase Cloud Messaging to `DeviceTokens`, which the mobile app registers via `POST /api/devices`. Push is enabled only when `FIREBASE_CREDENTIALS_JSON` or `FIREBASE_CREDENTIALS_PATH` is set; otherwise it is silently skipped. The Android app also needs `android/app/google-services.json`. The client's `PushService` creates the `orders` channel, which must match the server's `AndroidChannelId`.
- **Sounds:** `SoundService` generates notification sounds with Web Audio. It unlocks the `AudioContext` on the user's first tap, because browsers block audio before any user interaction.
- **Vendor onboarding:** a SuperAdmin creates vendors and sends email invitations (`EmailService`, SMTP via Brevo). The invited user completes registration through `AcceptInvitation`, which the client handles on the `setup-account` page.
- **Images** are resized with SkiaSharp and stored under `wwwroot/Resources/Images`. The DB stores the relative `/Resources/...` path, while seed data uses absolute Unsplash URLs. The client's `getImageUrl` handles both forms.

## Seed data

`DataAccess/SeedData/SeedData.cs` creates these users:
- `admin` / `admin1234` (SuperAdmin)
- `kupac` / `kupac1234` (Customer)
- `kurir1`, `kurir2` / `kurir1234` (Courier). These are seeded even when markets already exist.
- nine vendor users (`marko`, `jovana`, `nikola`, `milan`, `jelena`, `dragan`, `ana`, `bojan`, `ivana`) with the password `vendor1234` (VendorAdmin)

It also creates four markets, their stalls, the vendors, and their products. The markets, vendors, and products are seeded only when no market exists yet. Identity passwords are deliberately weak: at least 4 characters with a lowercase letter.

## Client architecture

- Each page is a lazy-loaded Ionic module, registered in `app-routing.module.ts`.
  - Pages that need a logged-in user use `AuthGuard`.
  - Role-restricted pages use `RoleGuard` with `data: { roles: [...] }`.
- `interceptors/token.interceptor.ts` attaches the JWT, which `AuthService` keeps in `localStorage` (`token`, `user`).
- **i18n:** `services/translate.service.ts` holds an inline dictionary per language (`sr`, `en`, `ru`). Templates use the `t` pipe (`'key' | t`) from `pipes/translate.pipe.ts`, exported through `SharedModule`. New user-facing text on customer pages needs keys in all three languages. The vendor and admin pages still mostly use hardcoded Serbian strings.
- `CartService` keeps the cart on the client, in `localStorage` (`cart`). `NotificationService` wraps the SignalR connection.
