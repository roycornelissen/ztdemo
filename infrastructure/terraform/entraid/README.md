# Entra ID app registrations

Standalone Terraform configuration that (re)creates the three Entra ID app
registrations used by Minibank, so the whole stack can be moved to a new
tenant without touching `envs/dev`. Deploy it separately:

```bash
cd infrastructure/terraform/entraid
terraform init
cp terraform.tfvars.example terraform.tfvars   # edit as needed
az login --tenant <new-tenant-id>               # target tenant context
terraform plan
terraform apply
```

## What it creates

| App registration | Source | Purpose |
|---|---|---|
| `minibank-accounts-api-<suffix>` | `src/AccountsApi` | Exposes the `Accounts.Read` delegated scope under `api://minibank-accounts-api`. |
| `minibank-payments-api-<suffix>` | `src/PaymentsApi` | Exposes the `Payment.Create` delegated scope under `api://minibank-payments-api`. |
| `minibank-client-<suffix>` | `src/MiniBankClient`, Swagger UI | Public client for the MSAL device-code flow and Swagger UI's browser-based authorization-code flow. |

Admin consent for the client's delegated permissions is granted automatically
(`var.grant_admin_consent`, default `true`) so the device-code flow doesn't
prompt for consent on first run. Set it to `false` if the deploying principal
isn't a Global/Privileged Role Administrator, and consent manually instead.

Swagger UI redeems authorization codes from its browser UI at
`/swagger/oauth2-redirect.html`. Terraform registers that path on each API's
fixed https launch profile port (`https://localhost:7137/...` for
AccountsApi, `https://localhost:7296/...` for PaymentsApi) on the
application's **Single-page application** platform. Change
`swagger_ui_redirect_uris` and the corresponding `launchSettings.json`
`applicationUrl` together if those ports are unavailable.

## After applying

Use the outputs to update the three `appsettings.json` files (or the
equivalent environment variables / user secrets) with the new tenant's IDs:

- `tenant_id` -> `Entra:TenantId` in all three apps.
- `accounts_api_client_id` -> `Entra:ClientId` in `src/AccountsApi`.
- `payments_api_client_id` -> `Entra:ClientId` in `src/PaymentsApi`.
- `client_client_id` -> `Entra:ClientId` in `src/MiniBankClient`, and also
  `SwaggerUi:ClientId` in `src/AccountsApi/appsettings.json` and
  `src/PaymentsApi/appsettings.json` (Swagger UI reuses the same public SPA
  app registration for its browser-based authorization-code + PKCE flow).

The `Entra:Audience` / `Entra:Domain` values only need to change if you also
change `accounts_api_identifier_uri` / `payments_api_identifier_uri` from
their defaults.

The API app registrations expose delegated scopes under their App ID URIs
(`api://minibank-accounts-api` and `api://minibank-payments-api` by default),
which is what `MiniBankClient` requests. Entra access tokens can still emit the
API application's client ID in the JWT `aud` claim, so the APIs validate both
their configured App ID URI and `Entra:ClientId`.
