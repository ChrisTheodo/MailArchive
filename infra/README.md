# MailArchive — Azure deployment

Hosted on **Azure Container Apps** in resource group `rg-emailarchive` (subscription *AzureSponsor-2400*, region *North Europe*).

```
                Internet (HTTPS, managed cert: archive.tbxdev.gr)
                               │
                ┌──────────────▼──────────────┐
                │  archive  (nginx + Vue SPA) │  external ingress, 1–3 replicas
                │  /api/* ──► proxy           │
                └──────────────┬──────────────┘
                               │ http://archive-api (internal only)
                ┌──────────────▼──────────────┐
                │ archive-api (.NET 10 API)   │  internal ingress, exactly 1 replica
                │  /app/storage ◄── Azure Files (PSTs + attachments)
                └──────────────┬──────────────┘
                               │ SSL
                toolbox.postgres.database.azure.com / mailarchive  (rg-bcm)
```

| Resource | Name | Purpose |
|---|---|---|
| Container Apps environment | `cae-emailarchive` | Consumption workload profile, logs to Log Analytics |
| Container app (web) | `archive` | nginx serving the Vue build, proxies `/api/` to the API — no CORS needed |
| Container app (API) | `archive-api` | ASP.NET Core API, internal only |
| Container Registry | `acremailarchivenee5ys` | Images `mailarchive-api`, `mailarchive-web` (admin user disabled) |
| Key Vault | `kv-emailarchive-nee5ys` | `db-connection`, `jwt-key` (RBAC, purge protection) |
| Storage account | `stemailarchivenee5ys` | Azure Files share `mailarchive-storage` (ZRS, 14-day soft delete) mounted at `/app/storage` |
| Managed identity | `id-emailarchive` | AcrPull + Key Vault Secrets User for both apps |
| Managed identity | `id-emailarchive-github` | GitHub Actions OIDC (AcrPush + Contributor on the RG) |
| Log Analytics | `log-emailarchive` | Container logs, 30-day retention |

## Configuration (API environment variables)

| Variable | Source |
|---|---|
| `ConnectionStrings__DefaultConnection` | Key Vault `db-connection` |
| `Jwt__Key` | Key Vault `jwt-key` (random 512-bit) |
| `Jwt__Issuer` / `Jwt__Audience` | `MailArchive` |
| `MailArchive__PstParserProvider` | `XstReader` |
| `MailArchive__MaxPageSize` / `EnableAuditLogs` | `100` / `true` |

The frontend is built with `VITE_API_BASE_URL=""`, so it calls relative `/api/...` URLs.

## CI/CD

`.github/workflows/deploy-azure.yml` runs on every push to `main` that touches `backend/` or `frontend/`, or when started manually. It builds both images, pushes them to ACR tagged with the commit SHA, updates both container apps, and smoke-tests `/healthz`.

Needs these **repository variables** (Settings → Secrets and variables → Actions → Variables). No secrets are required because sign-in uses OIDC:

| Variable | Value |
|---|---|
| `AZURE_CLIENT_ID` | `7d2a53c8-6218-4382-9c59-b21bf9f79b4a` |
| `AZURE_TENANT_ID` | `605c5981-8c5d-45bc-b086-192a91600f63` |
| `AZURE_SUBSCRIPTION_ID` | `e0654185-6903-4e10-adb2-dddaf8b7a781` |
| `ACR_NAME` | `acremailarchivenee5ys` |

You also need a GitHub **environment** called `production`, because the federated credential trusts `repo:ChrisTheodo/MailArchive:environment:production`. Add required reviewers there if you want manual approval before each deploy.

## Re-deploying the infrastructure

```bash
az deployment group create -g rg-emailarchive -f infra/main.bicep \
  -p apiImage=acremailarchivenee5ys.azurecr.io/mailarchive-api:<tag> \
     webImage=acremailarchivenee5ys.azurecr.io/mailarchive-web:<tag> \
```

`customDomain` (`archive.tbxdev.gr`) and `managedCertificateName` (`mc-cae-emailarchi-archive-tbxdev-g-8167`) default to the live values, so a redeploy keeps the HTTPS binding.

## Custom domain

- URL: **https://archive.tbxdev.gr**. Default URL: https://archive.thankfulbush-4942322e.northeurope.azurecontainerapps.io
- DNS records in tbxdev.gr: CNAME `archive` → `archive.thankfulbush-4942322e.northeurope.azurecontainerapps.io`, and TXT `asuid.archive` → `B910BADDFD3DC05B326C53CC85B7FB995F07C9428848E718620D834FCA74FF57`. Keep both, because the managed certificate (GeoTrust/DigiCert) renews automatically and needs them.
- HTTP redirects to HTTPS.

## Database

- Server `toolbox` (rg-bcm), database `mailarchive`, login **`mailarchive_app`**. This role owns the database, the `public` schema and all of its tables, and its password exists only in Key Vault `db-connection`.
- The existing data was kept: all 10 EF migrations were already applied, so the first start made no schema changes.
- An on-demand server backup `pre-mailarchive-aca-20261103` was taken before the role change.

## Operational notes

- **Keep the API at exactly one replica.** Imports run on an in-process background queue (`QueuedHostedService`), and `StaleImportRecoveryService` assumes a single instance.
- **Migrations and seeding run at startup.** `DataSeeder` creates `admin@example.com` / `Admin123!` if it's missing, and regenerates demo attachment PDFs in `/app/seed-storage`, which is not persistent. Change the seeded password and disable demo seeding in Production.
- **Existing attachment and PST files are not in Azure yet.** The database rows point to `storage/attachments/...` and `storage/imports/...` (plus 2 paths under a developer's `C:\Users\...`). Those files are on the developer machine that created them. To make downloads work, upload that machine's `MailArchive.API/storage/` contents to the `mailarchive-storage` share, keeping the `attachments/` and `imports/` folders.
- **Large PST uploads:** the API accepts up to 1 GB and nginx allows 1100 MB. Container Apps ingress has a request timeout of about 4 minutes, so very large uploads over slow links can fail. If that happens, move uploads to direct-to-Blob SAS uploads.
- **Postgres access:** `toolbox` allows "Azure services", which covers Container Apps. To tighten it, replace that rule with the environment's outbound IP.
- **Logs:** Portal → `archive-api` → Log stream, or Log Analytics table `ContainerAppConsoleLogs_CL`.
- **Dependency alert:** `System.Security.Cryptography.Pkcs 10.0.8` has known advisories (GHSA-846f-f764-76mx, high). Upgrade the package.
