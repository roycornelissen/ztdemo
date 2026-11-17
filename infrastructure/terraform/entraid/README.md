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
| `minibank-payments-api-<suffix>` | `src/PaymentsApi` | Exposes `Payment.Read` and `Payment.Write` delegated scopes under `api://minibank-payments-api`. |
| `minibank-client-<suffix>` | Swagger UI in both APIs | Public SPA client for browser-based authorization-code flow with PKCE. |

Admin consent for the client's delegated permissions is granted automatically
(`var.grant_admin_consent`, default `true`) so API permissions do not require
consent on first use. Set it to `false` if the deploying principal
isn't a Global/Privileged Role Administrator, and consent manually instead.

The client registration also supports native public clients and includes
Microsoft Graph `User.Read`. Swagger UI uses the SPA redirects and each API's
delegated scopes. PaymentsApi preselects `Payment.Read` for payment history.
To demo write-only access, deselect it and select `Payment.Write` in the
Authorize dialog before submitting a payment.

For existing deployments, Terraform reuses the `Payment.Create` permission ID
for `Payment.Write` and updates the managed consent grant. Sign in again after
applying to get a fresh token with the new scope names.

Swagger UI redeems authorization codes from its browser UI at
`/swagger/oauth2-redirect.html`. Terraform registers that path on each API's
fixed https launch profile port (`https://localhost:7137/...` for
AccountsApi, `https://localhost:7296/...` for PaymentsApi) on the
application's **Single-page application** platform. Change
`swagger_ui_redirect_uris` and the corresponding `launchSettings.json`
`applicationUrl` together if those ports are unavailable.

## After applying

Use the outputs to update both APIs' `appsettings.json` files (or the
equivalent environment variables / user secrets) with the new tenant's IDs:

- `tenant_id` -> `Entra:TenantId` in both APIs, and the tenant segment of
  `Entra:Authority` in PaymentsApi.
- `accounts_api_client_id` -> `Entra:ClientId` in `src/AccountsApi`.
- `payments_api_client_id` -> `Entra:ClientId` in `src/PaymentsApi`.
- `client_client_id` -> `SwaggerUi:ClientId` in both APIs (Swagger UI uses
  the same public SPA app registration for its browser-based
  authorization-code + PKCE flow).

Set `Entra:Audience` to the corresponding API's identifier URI and update
`Entra:Domain` if the tenant domain changes. If you customize the identifier URIs,
also update the scope string in AccountsApi's OpenAPI document transformer.
PaymentsApi derives its OpenAPI scope strings from `Entra:Audience`, `Entra:Scopes`
(`Payment.Read`), and `Entra:WriteScope` (`Payment.Write`).

The API app registrations expose delegated scopes under their App ID URIs
(`api://minibank-accounts-api` and `api://minibank-payments-api` by default),
which Swagger UI uses when requesting scopes. Entra access tokens can still emit the
API application's client ID in the JWT `aud` claim, so the APIs validate both
their configured App ID URI and `Entra:ClientId`.
