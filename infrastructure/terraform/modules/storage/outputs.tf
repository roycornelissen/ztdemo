output "storage_account_name" {
  description = "Name of the storage account."
  value       = azurerm_storage_account.this.name
}

output "storage_account_id" {
  description = "Resource ID of the storage account."
  value       = azurerm_storage_account.this.id
}

output "table_endpoint" {
  description = "Table service endpoint for identity-authenticated workload clients."
  value       = azurerm_storage_account.this.primary_table_endpoint
}

output "queue_endpoint" {
  description = "Queue service endpoint for identity-authenticated workload clients."
  value       = azurerm_storage_account.this.primary_queue_endpoint
}
