output "key_vault_name" {
  description = "Name of the Key Vault resource."
  value       = azurerm_key_vault.this.name
}

output "key_vault_id" {
  description = "Resource ID of the Key Vault."
  value       = azurerm_key_vault.this.id
}
