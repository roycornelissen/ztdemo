# Terraform migration scaffold

This folder implements the migration layout described in `../TERRAFORM_MIGRATION_PLAN.md`.

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
