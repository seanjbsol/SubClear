# SubClear

UK SaaS **subcontractor compliance register** for mid-tier main contractors. SubClear is the supply-chain chase layer — insurance, SSIP evidence, RAMS — not another SSIP scheme.

Contracts managers currently chase EL/PL/PI, SSIP certificates and RAMS over spreadsheets and WhatsApp. SubClear keeps a live, tenant-scoped register with traffic-light status, a chase log, a subcontractor upload portal (Pro), automated reminders (Pro), and an expert review queue (Pro).

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
| Humber Civils Ltd | `reviewer@demo.subclear.uk` | `DemoPassword123!` | Reviewer (staff) |
| Northern M&E Ltd | `owner@northern.demo.subclear.uk` | `DemoPassword123!` | Owner (second tenant) |

Humber Civils includes mixed traffic lights (green / amber / red) plus chase notes and two sample projects. Northern M&E has a single subcontractor that **must not** appear for Humber users.

## Domain (MVP)

- **Tenant** = main contractor organisation. Register creates a tenant and an Owner user.
- **Roles:** Owner, Admin, ContractsManager, Reviewer, Viewer. Viewer is read-only. Reviewer (or Owner/Admin) can approve or reject documents.
- Subcontractors, compliance documents (EL / PL / PI / SSIP / RAMS / CSCS / Other) with expiry, optional file bytes (portal uploads), chase log, optional projects.
- **Traffic lights** from required-pack expiries (rejected documents do not count; pending in-date documents are amber):
  - **Red** — missing required document, past expiry, marked expired, or rejected with nothing else on file
  - **Amber** — in date but expires within 30 days, present with no expiry date, or awaiting review
  - **Green** — required pack in date and approved
- **Dashboard:** non-compliant count, expiring in 30 days, chase queue, awaiting review.
- **Pack:** `GET /api/subcontractors/{id}/pack` — required-docs checklist for a sub, including review status.
- **Pro:** magic-link portal, automated chase emails, expert review queue.
- **Starter:** manual chase log and a capped subcontractor register.

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
| GET/POST | `/api/subcontractors/{id}/chases` | Chase log (Starter and Pro). |
| GET | `/api/chase-queue` | Red + amber subs. |
| GET | `/api/dashboard` | Counts + attention list. |
| GET/POST | `/api/projects` | Optional project links. |
| GET | `/api/billing/entitlements` | Plan / status / Pro flags from Qck (no Stripe). |
| POST | `/api/billing/checkout` | Owner/Admin. Proxies to a Qck checkout session. |
| POST | `/api/billing/portal` | Owner/Admin. Proxies to the Qck customer portal. |
| POST | `/api/subcontractors/{id}/portal-invites` | **Pro.** Email a magic-link upload portal. |
| GET | `/api/portal/{token}` | Anonymous. Pack for that invite. |
| POST | `/api/portal/{token}/documents` | Anonymous. Upload onto the tenant sub record. |
| GET/POST | `/portal/{token}` | HTML portal (no app to install). |
| GET | `/api/review-queue` | **Pro.** Pending documents. |
| POST | `/api/documents/{id}/review` | **Pro.** Owner/Admin/Reviewer approve or reject. |
| GET | `/api/directory` | Anonymised verified network. |
| POST | `/api/directory/link-requests` | Link a listing, or invite by email (**Pro** portal). |
| GET/PUT | `/api/chase-automation` | Cadence (default 7 days). PUT is **Pro**. |
| POST | `/api/chase-automation/run` | **Pro.** Owner/Admin. Send due chase emails now. |
| GET | `/api/email-log` | **Pro.** Every outbound send. |

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

- Entitlements default to `trialing` / plan `pro` (override with `SubscriptionApi:StubStatus` and `SubscriptionApi:StubPlan`)
- Checkout and portal return `https://billing.stub.qckapp.local/...` URLs
- Tenant upsert is a no-op log

Use this in CI and `dotnet test`. Do not enable stub in production. Development stubs **Pro** so portal, chase emails and review can be exercised locally. Set `SubscriptionApi:StubPlan=starter` to see upgrade gates.

### Plans

| | Starter | Pro |
| --- | --- | --- |
| Manual chase log | Yes | Yes |
| Subcontractor cap | 15 (configurable) | Unlimited |
| Magic-link portal | Upgrade | Yes |
| Automated chase emails | Upgrade | Yes |
| Expert review queue | Upgrade | Yes |

Pro actions return **402** with `upgradeHint` / `checkoutHint`: `POST /api/billing/checkout`.

## Subcontractor portal (magic link)

Owner, Admin or Contracts manager (Pro) posts `POST /api/subcontractors/{id}/portal-invites`. SubClear emails a private link that expires (default **7 days**). The subcontractor opens `/portal/{token}` in a browser — no app, no account — and uploads EL, PL, PI, SSIP or RAMS. Files land on **that tenant’s subcontractor record** with review status **Pending**.

Tokens are stored as SHA-256 hashes. Raw tokens appear once on the create response (and in the email). Expired links return **410 Gone**. Isolation still applies: another tenant cannot see the uploaded document.

`Portal:PublicBaseUrl` is used in emails (for example `https://app.subclear.example`). If it is empty, the API uses `SubscriptionApi:AppBaseUrl` or the current request origin.

If you already have a local SQLite file from an older schema, delete `apps/api/subclear.dev.db` so `EnsureCreated` rebuilds it.

## Email and automated chases

| Setting | Purpose |
| --- | --- |
| `Email:Provider` | `Console`, `File`, or `Smtp` |
| `Email:FileDirectory` | Where File writes `.txt` messages |
| `Email:SmtpHost` | Required for Smtp in non-development |
| `Chase:DefaultCadenceDays` | Default **7** (min 3, max 28 — not daily) |
| `Chase:BackgroundEnabled` | Hosted service; **off** in Development |

If SMTP is not configured, Development falls back to the **console** sender (and you can set `Email:Provider=File` to inspect messages under `apps/api/emails/`). Every send is written to `/api/email-log` and a chase note (`isAutomated: true`). Copy is plain UK English and only goes out when a required document is missing or expired.

`POST /api/chase-automation/run` sends due reminders for the current tenant (Pro). The background job does the same across tenants when enabled.

## Tests

Production requires `SubscriptionApi__BaseUrl` and `SubscriptionApi__ApiKey` (plus `Jwt__Key`). Never commit real keys.

On mobile, Settings loads `GET /api/billing/entitlements` and Owners/Admins open **Manage billing** or **Upgrade** with `Linking.openURL` on the proxied Qck URL.

## Tests

```bash
dotnet test SubClear.sln
```

Covers traffic-light rules, register → tenant, sub/document CRUD, mark-expired, pack, chase queue, viewer 403, second-tenant isolation, stub billing entitlements/checkout/portal, 402 when the stub subscription is not active, portal tokens (including expiry), the chase email job and cadence, review approve/reject, and Starter 402 gates with an upgrade path.

## Production notes

- No secrets belong in git. Use environment variables / a secret store for `Jwt__Key`, `SubscriptionApi__ApiKey`, and SQL credentials.
- Swap SQLite to SQL Server as above; add migrations; turn off `Seed:Enabled` in production or replace the demo seeder.
- File **content** can be stored from the portal (`App_Data/uploads`). Plug in blob storage later via `StorageKey`.
- Email uses Console or File until SMTP is configured. Do not enable noisy daily chases — the default cadence is weekly.
