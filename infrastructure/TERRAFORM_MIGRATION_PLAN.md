# Terraform Migration Plan — `infrastructure/minibank`

This plan consolidates the current mix of Bicep modules (`ca-appgw/`) and
Azure CLI PowerShell scripts (`00-`…`05-*.ps1`, `deploy-infra.ps1`) into a
single Terraform codebase.

Guidance below follows the repo's installed Terraform agent skills:
- `.agents/skills/terraform/skills/terraform-style-guide` — HCL style, file
  layout, naming, variable/output conventions.
- `.agents/skills/terraform/skills/azure-verified-modules` — AVM rules for
  any module we author or consume (provider pinning, `for_each` map/set
  keys, sensitive variable/output handling, `terraform-docs`, etc.).
- `.agents/skills/terraform/skills/terraform-search-import` — how to bring
  the already-deployed Azure resources under Terraform management if we
  decide not to redeploy from scratch.

## 1. Current-state inventory

Deployed by `deploy-infra.ps1` (resource group `rg-minibank`, region
`westeurope`), in this order:

| # | Source | Resources |
|---|--------|-----------|
| 1 | `deploy-infra.ps1` (inline `az identity create`) | 3 user-assigned managed identities: `id-accounts-api`, `id-payments-api`, `id-processing` |
| 2 | `00-create-vnet-subnets.ps1` | VNet `vnet-payments` (`10.0.0.0/16`) + subnets `snet-apps` (delegated `Microsoft.App/environments`), `snet-appgw`, `AzureFirewallSubnet`, `snet-pep` |
| 3 | `01-create-storage.ps1` | Storage account `stminibank` (tables `accounts`/`transactions`, queue `payments`), role assignments to the 3 identities, 2 private endpoints (table, queue) + matching private DNS zones/links |
| 4 | `01-create-keyvault.ps1` | Key Vault `kv-minibank`, 2 secrets (`payments-client-secret`, `accounts-client-secret`), role assignments (current user + 3 identities), 1 private endpoint + private DNS zone/link |
| 5 | `02-create-nsgs-and-routes.ps1` | NSGs `nsg-containerapps` / `nsg-privateendpoints` + rules, subnet associations |
| 6 | `05-deploy-firewall.ps1` | Public IP, Azure Firewall `afw-minibank`, Firewall Policy `afwp-minibank` + rule collection, route table `rt-firewall` + routes + subnet association |
| 7 | `ca-appgw/main.bicep` + modules | Log Analytics `law-minibank`; ACA environment `env-minibank-1` (VNet-injected, internal); 3 Container Apps (`ca-accountapi-minibank`, `ca-payments`, `ca-processing`) pulling via UMI `id-containeruser-minibank`; cross-RG `AcrPull` role assignment against ACR `minibank` in `rg-minibank-dev`; private DNS zone for the ACA default domain; Application Gateway `agw-minibank` + public IP + private link config |

## 2. Target repo layout

```
infrastructure/
  terraform/
    modules/
      identities/         # 3x azurerm_user_assigned_identity + containeruser
      networking/          # vnet + 4 subnets
      security/            # NSGs, route table, firewall + policy
      keyvault/            # KV + secrets + role assignments + private endpoint
      storage/             # storage account, tables, queue, role assignments, private endpoints
      monitoring/          # log analytics workspace
      acr-rbac/            # data source on existing ACR + AcrPull role assignment
      container-platform/  # ACA environment + reusable container-app child module (for_each)
      appgateway/           # public IP + app gateway + ACA private DNS zone
    envs/
      dev/
        terraform.tf        # required_version + required_providers
        providers.tf
        backend.tf
        main.tf              # wires modules together, mirrors deploy-infra.ps1 order
        variables.tf
        outputs.tf
        terraform.tfvars.example   # committed; real terraform.tfvars is gitignored
      # prod/ added later, same shape, own backend key
```

Each module follows the style-guide file split (`terraform.tf`, `main.tf`,
`variables.tf`, `outputs.tf`, `locals.tf` only if needed) and the AVM rules
we choose to apply even though these are internal, non-published modules:
alphabetical variables (required first), explicit `type` + `description` on
every variable/output, `sensitive = true` on secret-bearing values, `for_each`
over `count` for the 3 container apps (map keyed by app name), no
`provider` blocks inside child modules.

### Reuse Azure Verified Modules where it saves work

Before hand-rolling a module, check the public registry for an AVM resource
module that already covers it (pin an explicit version, `~>` constraint):
- `Azure/avm-res-network-virtualnetwork/azurerm`
- `Azure/avm-res-keyvault-vault/azurerm`
- `Azure/avm-res-storage-storageaccount/azurerm`
- `Azure/avm-res-operationalinsights-workspace/azurerm`
- `Azure/avm-res-managedidentity-userassignedidentity/azurerm`
- `Azure/avm-res-network-applicationgateway/azurerm`
- `Azure/avm-res-network-azurefirewall/azurerm`

Only hand-write a module when no AVM module exists or it doesn't fit
(e.g. the Container Apps + private DNS + App Gateway wiring is bespoke
enough to author directly). Verify exact module names/versions on the
Terraform Registry before pinning, since AVM coverage changes frequently.

## 3. Key decisions

1. **Provider & version pins** (`terraform.tf` per env):
   ```hcl
   terraform {
     required_version = "~> 1.14"
     required_providers {
       azurerm = {
         source  = "hashicorp/azurerm"
         version = "~> 4.0"
       }
     }
   }
   ```
2. **Remote state**: bootstrap a dedicated `rg-tfstate` + storage account +
   container (versioning enabled) once, outside the main config. Use the
   `azurerm` backend with `use_azuread_auth = true` (no shared keys), one
   state file per environment (`dev.terraform.tfstate`, later
   `prod.terraform.tfstate`).
3. **Secrets**: `payments_client_secret` / `accounts_client_secret` become
   `sensitive = true` variables with **no default**, supplied via
   `TF_VAR_*` environment variables or CI/CD secret store — never written
   to a committed `.tfvars` file.
4. **Existing resources — redeploy fresh vs. import**: since `rg-minibank`
   is a demo/dev environment, the default plan is:
   - Tear down the RG created by the Bicep/CLI scripts.
   - Stand up the same resources cleanly via `terraform apply`.
   - This avoids state-drift/import edge cases (e.g. Key Vault purge
     protection, private endpoint DNS zone group quirks).

   If the running demo must be preserved instead, use the
   `terraform-search-import` workflow: check `terraform query`
   (list-resource) support for each `azurerm_*` type via
   `./scripts/list_resources.sh azurerm`; for anything unsupported, fall
   back to `aztfexport` (Azure's export-to-Terraform tool) or manual
   `import` blocks, then reconcile generated config against the
   hand-written modules above and run `terraform plan` until it shows no
   diff.
5. **Cross-RG dependency**: ACR `minibank` lives in `rg-minibank-dev` and is
   not managed by this config — reference it via
   `data "azurerm_container_registry"` and scope the `AcrPull`
   `azurerm_role_assignment` to its data-source `id`.
6. **CI/CD**: GitHub Actions workflow — `terraform fmt -check` →
   `terraform validate` → `tflint` (azurerm ruleset) → `checkov` →
   `terraform plan` (posted as PR comment) → manual-approval
   `terraform apply` on merge to `main`.
7. **Cleanup**: only delete `*.bicep` / `*.ps1` under `infrastructure/minibank`
   after a `terraform apply` against a real subscription succeeds and a
   smoke test passes (App Gateway `/payment` and `/accounts` paths return
   200s, container apps healthy, private endpoints resolve correctly).

## 4. Work breakdown (tracked as session todos — see below)

1. Inventory resources *(done)*
2. Choose Terraform layout *(done)*
3. Bootstrap remote state backend
4. Module: networking
5. Module: identities
6. Module: monitoring (Log Analytics)
7. Module: security (NSGs/route table/firewall)
8. Module: keyvault
9. Module: storage
10. Module: acr-rbac
11. Module: container-platform (ACA env + 3 apps via `for_each`)
12. Module: appgateway
13. Root module wiring (`envs/dev`)
14. Decide/execute secrets handling
15. Decide fresh-deploy vs. import, execute
16. Validate (plan/apply + smoke test)
17. Add CI/CD pipeline
18. Decommission old Bicep/PowerShell files

> Note: this plan document is committed to the repo for durability. The
> live task list with status/dependencies is tracked in this session's todo
> database and is not itself persisted to git.

## 5. References

- HashiCorp Terraform style guide: https://developer.hashicorp.com/terraform/language/style
- Azure Verified Modules specs: https://azure.github.io/Azure-Verified-Modules/specs/module-specs/
- `azurerm` backend docs: https://developer.hashicorp.com/terraform/language/backend/azurerm
- Terraform Search / bulk import: https://developer.hashicorp.com/terraform/language/import/bulk
- Azure Export for Terraform (aztfexport): https://github.com/Azure/aztfexport
