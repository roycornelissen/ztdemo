# Terraform infrastructure

This folder provisions the Azure infrastructure for MiniBank. Aspire handles
local orchestration; these Terraform configurations model the Azure deployment,
including Container Apps, workload identities, networking and storage.

## AVM alignment

The deployment is intentionally structured around the AVM guidance used in this repository:

- explicit `required_version` and `required_providers` blocks in each environment/module
- `type` and `description` for every input/output
- lower snake_case resource and module names
- module-level separation for environment wiring and Azure resource grouping
- documentation generated via `terraform-docs` through `.terraform-docs.yml`

This is still a migration scaffold rather than a published AVM module repository, so the code keeps the current AzureRM implementation details that are already validated in the repo while tracking toward an AVM-first shape.

## Layout

- `envs/dev` contains the root configuration and environment wiring.
- `entraid` contains the separately deployed API and Swagger UI app registrations.
- `modules/*` contains the Terraform modules that match the Minibank deployment order and Azure resource grouping.

## Initial setup

1. Copy `envs/dev/terraform.tfvars.example` to `envs/dev/terraform.tfvars`.
2. Supply the required secret values and, when available, the current Azure AD object ID.
3. Run:

```bash
cd infrastructure/terraform/envs/dev
terraform init
terraform plan
```

The backend block is intentionally a template for the dedicated AzureRM remote state storage account.
Update the backend fields before the first production apply.

Publish the workload images with `dotnet publish -t:PublishContainer`, using the
registry, repository names and tags referenced in `envs/dev/main.tf`. See the
[container publishing commands](../../README.md#container-images). Terraform
does not build images. The firewall and Application Gateway modules are
intentionally not enabled in the development environment.

## Zero Trust storage sample

Workload grants are scoped to individual tables/queues, not the storage account.
The Container Apps module receives nonsecret workload identity client IDs and
storage endpoints; applications use managed identities outside Development.
The module uses AzAPI to set each app's
[managed identity availability](https://learn.microsoft.com/azure/container-apps/managed-identity#control-managed-identity-availability).
The shared ACR pull identity is set to `None`: Azure can still pull images with
it, but containers cannot request its tokens. Each app's workload identity is
set to `Main`, so only its main containers can use it.

See [the sample walkthrough](../../docs/zero-trust-samples.md#5-separate-local-emulator-trust-from-deployed-workload-trust)
for the role matrix, mock-provider tests, migration impact, and the locked
AzureRM provider's Shared Key provisioning limitation. Public storage access and
account keys are not disabled by this sample; private endpoints do not imply
that public access is blocked.
