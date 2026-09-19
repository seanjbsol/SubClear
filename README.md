# SubClear

UK SaaS **subcontractor compliance register** for mid-tier main contractors. SubClear is the supply-chain chase layer — insurance, SSIP evidence, RAMS — not another SSIP scheme.

Contracts managers currently chase EL/PL/PI, SSIP certificates and RAMS over spreadsheets and WhatsApp. SubClear keeps a live, tenant-scoped register with traffic-light status, a chase log, and a mobilisation pack checklist.

## Monorepo

| Path | Stack |
| --- | --- |
| `/apps/api` | ASP.NET Core 10 Web API, EF Core 10, JWT |
| `/apps/mobile` | React Native (Expo managed) |
| `/apps/api.tests` | xUnit integration and unit tests |
| `SubClear.sln` | Solution |

## Local development

### API

Requires the .NET 10 SDK (LTS, TFM `net10.0`).

```bash
cd apps/api
dotnet run --urls http://localhost:5151
```

- Health: `GET http://localhost:5151/health`
- Swagger (Development): `http://localhost:5151/swagger`

SQLite file `apps/api/subclear.dev.db` is created on first run and seeded automatically.

### Mobile

```bash
cd apps/mobile
npx expo start
```

Set the API base URL with **`EXPO_PUBLIC_API_URL`** (no trailing slash):

```bash
EXPO_PUBLIC_API_URL=http://localhost:5151 npx expo start
```

- iOS simulator: `http://localhost:5151`
- Android emulator: `http://10.0.2.2:5151`
- Physical device: your machine's LAN URL, e.g. `http://192.168.1.10:5151`

### Demo tenant

Seeded on startup (idempotent):

| Organisation | Email | Password | Role |
| --- | --- | --- | --- |
| Humber Civils Ltd | `owner@demo.subclear.uk` | `DemoPassword123!` | Owner |
| Humber Civils Ltd | `contracts@demo.subclear.uk` | `DemoPassword123!` | Contracts manager |
| Humber Civils Ltd | `viewer@demo.subclear.uk` | `DemoPassword123!` | Viewer |
| Northern M&E Ltd | `owner@northern.demo.subclear.uk` | `DemoPassword123!` | Owner (second tenant) |

Humber Civils includes mixed traffic lights (green / amber / red) plus chase notes and two sample projects. Northern M&E has a single subcontractor that **must not** appear for Humber users.

## Domain (MVP)

- **Tenant** = main contractor organisation. Register creates a tenant and an Owner user.
- **Roles:** Owner, Admin, ContractsManager, Viewer. Viewer is read-only.
- Subcontractors, compliance documents (EL / PL / PI / SSIP / RAMS / CSCS / Other) with expiry and file **metadata**, chase log (date, note, outcome — list only; email automation is out of scope), optional projects.
- **Traffic lights** from required-pack expiries:
  - **Red** — missing required document, past expiry, or marked expired
  - **Amber** — in date but expires within 30 days, or present with no expiry date
  - **Green** — required pack in date
- **Dashboard:** non-compliant count, expiring in 30 days, chase queue.
- **Pack:** `GET /api/subcontractors/{id}/pack` — required-docs checklist for a sub.

## Auth and tenant isolation

JWT access tokens include:

- `sub` / name identifier — user id
- **`tenant_id`** — current organisation
- **`role`** — Owner, Admin, ContractsManager, or Viewer

Isolation is enforced three ways:

1. **JWT** — `TenantResolutionMiddleware` binds `tenant_id` + role onto the request.
2. **EF Core global query filters** — `TenantId == CurrentTenantId` on every tenant-owned entity (memberships, subcontractors, documents, chase logs, projects).
3. **Explicit checks** — writes stamp/verify `TenantId` in `SaveChanges` and controllers call `TenantGuard.Ensure`.

Login and register look up memberships with `IgnoreQueryFilters()` because there is no tenant yet. Users are global (unique email); business data is never queried without a tenant.

A second tenant logging in receives a 404 for another tenant's subcontractor ids and never sees those rows in list/dashboard/chase-queue queries.

Configure production signing with `Jwt__Key` (32+ characters). Development falls back to a well-known local key that is **not** a secret.

## SQL Server vs SQLite

Default provider is **SQLite** for local/dev (`Database:Provider=Sqlite`).

To use SQL Server, set:

```bash
Database__Provider=SqlServer
Database__ConnectionString="Server=localhost,1433;Database=SubClear;User Id=sa;Password=...;TrustServerCertificate=True;Encrypt=True"
```

The same EF Core model is used. `EnsureCreated` is fine for the SQLite scaffold. For SQL Server in a real environment, add a migration against that provider:

```bash
cd apps/api
dotnet ef migrations add InitialSqlServer -- --provider SqlServer
dotnet ef database update
```

SQLite and SQL Server migrations are not interchangeable (types, limits). Keep provider-specific migrations or generate them per environment. Do not commit connection strings or `sa` passwords.

## API surface (authenticated unless noted)

| Method | Path | Notes |
| --- | --- | --- |
| GET | `/health` | Anonymous. Liveness + database connect. |
| POST | `/api/auth/register` | Creates tenant + Owner. |
| POST | `/api/auth/login` | JWT with `tenant_id` and role. |
| GET | `/api/me` | Current user + organisation. |
| GET/POST | `/api/subcontractors` | List / create. |
| GET/PUT/DELETE | `/api/subcontractors/{id}` | Delete: Owner/Admin. |
| GET | `/api/subcontractors/{id}/pack` | Required docs checklist. |
| GET/POST | `/api/subcontractors/{id}/documents` | Metadata create. |
| PUT / POST / DELETE | `/api/documents/{id}` | `POST .../mark-expired`. |
| GET/POST | `/api/subcontractors/{id}/chases` | Chase log. |
| GET | `/api/chase-queue` | Red + amber subs. |
| GET | `/api/dashboard` | Counts + attention list. |
| GET/POST | `/api/projects` | Optional project links. |
| GET | `/api/billing/entitlements` | Plan / status from Qck (no Stripe). |
| POST | `/api/billing/checkout` | Owner/Admin. Proxies to a Qck checkout session. |
| POST | `/api/billing/portal` | Owner/Admin. Proxies to the Qck customer portal. |

Write endpoints require Owner, Admin, or ContractsManager. CORS is open in this scaffold; restrict origins before production.

## Billing (QckApp Subscription API)

SubClear does **not** call Stripe. Billing is a small `SubscriptionClient` (`HttpClient`) against the central **QckApp Subscription API**.

| Setting | Purpose |
| --- | --- |
| `SubscriptionApi:BaseUrl` | Qck base URL, no trailing slash |
| `SubscriptionApi:ApiKey` | Sent as the `X-Api-Key` header |
| `SubscriptionApi:ProductCode` | Always `SubClear` |
| `SubscriptionApi:UseStub` | `true` for CI/tests/local without Qck |
| `SubscriptionApi:AppBaseUrl` | Optional public app origin for checkout success/cancel and portal return |

Live calls (never from the mobile app):

- `PUT/POST {BaseUrl}/api/v1/tenants` — upsert name, owner email, `externalTenantId` (the SubClear tenant id) on register and before checkout
- `GET {BaseUrl}/api/v1/entitlements/SubClear/{tenantId}`
- `POST {BaseUrl}/api/v1/checkout/sessions`
- `POST {BaseUrl}/api/v1/portal/sessions`

Authenticated tenant routes that need billing (dashboard, subcontractors, documents, chase, projects) check entitlements. If the status is not `active` or `trialing`, the API returns **402** with `checkoutHint: "POST /api/billing/checkout"`. `/health`, auth, `/api/me`, and `/api/billing/*` stay reachable so an organisation can still sign in and upgrade.

### Stub mode

Set `SubscriptionApi:UseStub=true` (the Development default). No Qck HTTP calls are made:

- Entitlements default to `trialing` / plan `stub` (override with `SubscriptionApi:StubStatus`)
- Checkout and portal return `https://billing.stub.qckapp.local/...` URLs
- Tenant upsert is a no-op log

Use this in CI and `dotnet test`. Do not enable stub in production.

Production requires `SubscriptionApi__BaseUrl` and `SubscriptionApi__ApiKey` (plus `Jwt__Key`). Never commit real keys.

On mobile, Settings loads `GET /api/billing/entitlements` and Owners/Admins open **Manage billing** or **Upgrade** with `Linking.openURL` on the proxied Qck URL.

## Tests

```bash
dotnet test SubClear.sln
```

Covers traffic-light rules, register → tenant, sub/document CRUD, mark-expired, pack, chase queue, viewer 403, second-tenant isolation, stub billing entitlements/checkout/portal, and 402 when the stub subscription is not active.

## Production notes

- No secrets belong in git. Use environment variables / a secret store for `Jwt__Key`, `SubscriptionApi__ApiKey`, and SQL credentials.
- Swap SQLite to SQL Server as above; add migrations; turn off `Seed:Enabled` in production or replace the demo seeder.
- File **content** is not stored in MVP — only metadata (name, content type, size). Plug in blob storage later via `StorageKey`.
- Email chase automation is explicitly out of MVP; the chase log is the system of record for what was asked and when.
