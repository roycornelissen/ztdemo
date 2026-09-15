data "azuread_client_config" "current" {}

output "tenant_id" {
  description = "Tenant ID to use for 'Entra:TenantId' in all three appsettings.json files."
  value       = data.azuread_client_config.current.tenant_id
}

output "tenant_domain" {
  description = "Verified domain name to use for 'Entra:Domain' in AccountsApi/PaymentsApi appsettings.json."
  value       = var.tenant_domain
}

output "accounts_api_client_id" {
  description = "Client (application) ID to use for 'Entra:ClientId' in src/AccountsApi/appsettings.json."
  value       = azuread_application.accounts_api.client_id
}

output "accounts_api_identifier_uri" {
  description = "App ID URI exposed by the Accounts API. Used in client scope strings and accepted by the API as an audience."
  value       = var.accounts_api_identifier_uri
}

output "payments_api_client_id" {
  description = "Client (application) ID to use for 'Entra:ClientId' in src/PaymentsApi/appsettings.json."
  value       = azuread_application.payments_api.client_id
}

output "payments_api_identifier_uri" {
  description = "App ID URI exposed by the Payments API. Used in client scope strings and accepted by the API as an audience."
  value       = var.payments_api_identifier_uri
}

output "client_client_id" {
  description = "Client (application) ID to use for 'Entra:ClientId' in src/MiniBankClient/appsettings.json."
  value       = azuread_application.client.client_id
}

output "client_requested_scopes" {
  description = "Full scope strings the MiniBankClient console app requests, for reference."
  value = [
    "user.read",
    "${var.accounts_api_identifier_uri}/Accounts.Read",
    "${var.payments_api_identifier_uri}/Payment.Create",
  ]
}
